using System.Text.Json;
using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Sessions;
using Burdock.Application.UseCases.Sync;
using Burdock.Contracts.Content;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Infrastructure.Sync;
using Burdock.Server.Modules.Content.Application;
using Burdock.Server.Modules.Content.Domain;
using Burdock.Server.Modules.Identity.Application;
using Burdock.Server.Modules.Identity.Domain;
using Burdock.TestKit;
using Burdock.TestKit.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Burdock.Sync.EndToEnd.Tests;

/// <summary>The API host on a PostgreSQL 18 container, configured the way the development machine runs it, with relaxed rate limits.</summary>
public sealed class SyncHostFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("burdock_e2e")
        .WithUsername("burdock")
        .WithPassword("burdock-test")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        Factory = new SyncApiFactory(_container.GetConnectionString());
    }

    public async ValueTask DisposeAsync()
    {
        Factory?.Dispose();
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class SyncApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Burdock", connectionString);
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("Auth:AllowInsecureHttp", "true");
            builder.UseSetting("Auth:RefreshTokenReuseLeeway", "00:00:00");
            builder.UseSetting("Content:ServeStoreLocally", "true");
            // Pack URLs must point at the in-memory host so the gateway downloads through the same test server.
            builder.UseSetting("Content:PublicBaseUrl", "http://localhost/v1/content/store/");
            builder.UseSetting("Content:StorePath", Path.Combine(Path.GetTempPath(), "burdock-e2e-packs-" + Guid.NewGuid().ToString("N")));
            foreach (var policy in new[] { "IdentityCreate", "Token", "AnonIp", "User", "UserWrite", "ProgressBatch" })
            {
                builder.UseSetting("RateLimits:" + policy, "1000/00:01:00");
            }
        }
    }
}

/// <summary>
/// Phase 2 exit criterion (spec 17): two installations of one account converge on progress and settings without loss
/// on conflict, through the real <see cref="HttpSyncGateway"/>, the real use cases and the real API. Attaching the
/// second installation to the first account is done through the Identity stores, which is the step account linking
/// will perform later (spec 09 section 3.3); everything else goes over HTTP.
/// </summary>
public sealed class TwoDeviceSyncTests(SyncHostFixture host) : IClassFixture<SyncHostFixture>
{
    private static readonly Guid InstallationA = Guid.Parse("0199a4c0-0000-7000-8000-00000000e2a1");
    private static readonly Guid InstallationB = Guid.Parse("0199a4c0-0000-7000-8000-00000000e2b2");
    private static readonly WordEntry Journey = Entry("journey", "подорож", "0199a500-0000-7000-8000-00000000e201");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_installations_of_one_account_converge_on_progress_settings_and_content()
    {
        var learnerId = LearnerId.From(Guid.NewGuid());
        var learner = new FakeLearnerContext(learnerId, LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A2, []));
        var clock = new FakeClock(host.Factory.Services.GetRequiredService<Burdock.Server.Shared.Time.IClock>().UtcNow, TestTimeZones.KyivFixed);
        using var a = new Device(host.Factory.CreateClient(), InstallationA, learner, clock);
        using var b = new Device(host.Factory.CreateClient(), InstallationB, learner, clock);

        // 1. The first installation registers under the local learner id and pushes its first review.
        (await a.Grade.ExecuteAsync(Choice(Journey), new ExerciseOutcome(ExerciseMode.ChoiceTargetToNative, Correct: true, TimeSpan.FromSeconds(2)), Ct)).IsSuccess.ShouldBeTrue();
        var firstOnA = await a.Sync.ExecuteAsync(Ct);

        firstOnA.Online.ShouldBeTrue(firstOnA.Error);
        firstOnA.Error.ShouldBeNull();
        firstOnA.Progress!.Pushed.ShouldBe(1);
        a.State.Values[SyncUseCase.AccountKey].ShouldBe(learnerId.Value.ToString("D"));
        a.Outbox.Events.ShouldBeEmpty();

        // 2. The second installation joins the same account (the step account linking will perform) and pulls the review.
        await AttachInstallationAsync(b.Credentials.Credential, learnerId);
        var firstOnB = await b.Sync.ExecuteAsync(Ct);

        firstOnB.Online.ShouldBeTrue(firstOnB.Error);
        b.State.Values[SyncUseCase.AccountKey].ShouldBe(learnerId.Value.ToString("D"));
        firstOnB.Progress!.Pulled.ShouldBe(1);
        firstOnB.Progress.Applied.ShouldBe(1);
        var cardOnA = (await a.Cards.GetByLexemeAsync(learnerId, Journey.Id, Ct)).ShouldNotBeNull();
        var cardOnB = (await b.Cards.GetByLexemeAsync(learnerId, Journey.Id, Ct)).ShouldNotBeNull();
        cardOnB.Id.ShouldBe(cardOnA.Id);
        cardOnB.State.ShouldBe(cardOnA.State);
        cardOnB.Due.ShouldBe(cardOnA.Due);
        b.Reviews.Entries.ShouldHaveSingleItem().Id.ShouldBe(a.Reviews.Entries.Single().Id);

        // 3. Both change the same setting offline; after the passes both hold the later stamp and the other device's extra key.
        a.Settings.SetLocal("settings.level", "\"B1\"", clock.UtcNow);
        b.Settings.SetLocal("settings.level", "\"B2\"", clock.UtcNow.AddSeconds(5));
        b.Settings.SetLocal("settings.gamification", "false", clock.UtcNow);
        (await a.Sync.ExecuteAsync(Ct)).Settings!.SentToServer.ShouldBe(1);
        (await b.Sync.ExecuteAsync(Ct)).Settings!.SentToServer.ShouldBe(2);
        var settingsOnA = (await a.Sync.ExecuteAsync(Ct)).Settings!;

        settingsOnA.AppliedFromServer.ShouldBe(2);
        a.Settings.Entries["settings.level"].ValueJson.ShouldBe("\"B2\"");
        a.Settings.Entries["settings.level"].ClientId.ShouldBe(InstallationB.ToString("N"));
        a.Settings.Entries["settings.gamification"].ValueJson.ShouldBe("false");
        b.Settings.Entries["settings.level"].ValueJson.ShouldBe("\"B2\"");
        (await b.Sync.ExecuteAsync(Ct)).Settings!.SentToServer.ShouldBe(0);

        // 4. A review on the second installation reaches the first; afterwards nothing new flows either way.
        (await b.Grade.ExecuteAsync(Choice(Journey), new ExerciseOutcome(ExerciseMode.ChoiceTargetToNative, Correct: true, TimeSpan.FromSeconds(1)), Ct)).IsSuccess.ShouldBeTrue();
        (await b.Sync.ExecuteAsync(Ct)).Progress!.Pushed.ShouldBe(1);
        var secondOnA = await a.Sync.ExecuteAsync(Ct);
        var quietOnA = await a.Sync.ExecuteAsync(Ct);

        secondOnA.Progress!.Applied.ShouldBe(1);
        (await a.Cards.GetByLexemeAsync(learnerId, Journey.Id, Ct))!.Reps.ShouldBe(2);
        a.Reviews.Entries.Count.ShouldBe(2);
        quietOnA.Progress!.Pulled.ShouldBe(0);
        quietOnA.Progress.Pushed.ShouldBe(0);

        // 5. A pack published on the server reaches a device through the manifest, the SHA-256 check and the importer; the next pass sees 304.
        await PublishPackAsync(Document(Journey, Entry("ticket", "квиток", "0199a500-0000-7000-8000-00000000e202")));
        var refreshed = await a.Sync.ExecuteAsync(Ct);
        var unchanged = await a.Sync.ExecuteAsync(Ct);

        refreshed.Content!.ManifestChanged.ShouldBeTrue();
        refreshed.Content.PacksImported.ShouldBe(1);
        refreshed.Content.Failed.ShouldBeEmpty();
        (await a.Content.GetAsync(Journey.Id, Ct)).ShouldNotBeNull();
        (await a.Content.GetImportedPackVersionsAsync(Ct))["uk-en.a2.travel"].ShouldBe(1);
        unchanged.Content!.ManifestChanged.ShouldBeFalse();
    }

    [Fact]
    public async Task A_fresh_installation_proposing_a_taken_learner_id_stays_local()
    {
        var learnerId = LearnerId.From(Guid.NewGuid());
        var learner = new FakeLearnerContext(learnerId, LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A2, []));
        var clock = new FakeClock(host.Factory.Services.GetRequiredService<Burdock.Server.Shared.Time.IClock>().UtcNow, TestTimeZones.KyivFixed);
        using var first = new Device(host.Factory.CreateClient(), Guid.NewGuid(), learner, clock);
        using var reinstalled = new Device(host.Factory.CreateClient(), Guid.NewGuid(), learner, clock);

        (await first.Sync.ExecuteAsync(Ct)).Online.ShouldBeTrue();
        var refused = await reinstalled.Sync.ExecuteAsync(Ct);

        refused.Online.ShouldBeFalse();
        refused.Error.ShouldBe("sync.learner_id_taken");
        reinstalled.State.Values.ShouldNotContainKey(SyncUseCase.AccountKey);
    }

    private async Task AttachInstallationAsync(InstallationCredential credential, LearnerId learnerId)
    {
        using var scope = host.Factory.Services.CreateScope();
        var installations = scope.ServiceProvider.GetRequiredService<IInstallationStore>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IIdentityUnitOfWork>();
        var now = scope.ServiceProvider.GetRequiredService<Burdock.Server.Shared.Time.IClock>().UtcNow;
        var secret = InstallationSecret.Parse(credential.Secret);
        secret.IsSuccess.ShouldBeTrue(secret.Error?.Code);
        var installation = Installation.Register(credential.InstallationId, learnerId, secret.Value, ClientPlatform.Android, "1.0.0", now);
        installation.IsSuccess.ShouldBeTrue(installation.Error?.Code);
        await installations.AddAsync(installation.Value, Ct);
        await unitOfWork.CommitAsync(Ct);
    }

    private async Task PublishPackAsync(ContentPackDocument document)
    {
        using var scope = host.Factory.Services.CreateScope();
        var publish = scope.ServiceProvider.GetRequiredService<PublishPackUseCase>();
        var json = JsonSerializer.SerializeToUtf8Bytes(document, ContentPackJsonContext.Default.ContentPackDocument);
        var identity = new PackIdentity(document.PackId, LanguagePair.UkrainianToEnglish, document.Level, document.Topic, document.Version);
        var published = await publish.ExecuteAsync(new PublishPackCommand(identity, json, "1.0.0", [new PackAttribution("test", "CC0-1.0", null, null)]), Ct);
        published.IsSuccess.ShouldBeTrue(published.Error?.Code);
    }

    private static ContentPackDocument Document(params WordEntry[] entries) => new(
        ContentPackDocument.CurrentSchemaVersion,
        "uk-en.a2.travel",
        "uk-en",
        "A2",
        "travel",
        1,
        new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero),
        new ContentProvenanceDocument("test", "published", null, null, []),
        entries.Select(e => new ContentEntryDocument(e.Id.Value, e.Lexeme.Lemma, "noun", "A2", ["travel"], e.Translation, [], [e.Lexeme.Lemma], null, null, null, null, null)).ToArray());

    private static WordEntry Entry(string lemma, string translation, string id)
        => new(LexemeBuilder.ALexeme().WithLemma(lemma).AtLevel(CefrLevel.A2).InTopics("travel").WithId(Guid.Parse(id)).Build(), translation, [], [lemma], null, null, null, null);

    private static ChoiceItem Choice(WordEntry entry) => new(entry, ExerciseMode.ChoiceTargetToNative, entry.Lexeme.Lemma, [entry.Translation, "x", "y", "z"], 0);

    private sealed class RandomIds : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    /// <summary>One installation: the real gateway over its own HttpClient, in-memory local stores, the real use cases.</summary>
    private sealed class Device : IDisposable
    {
        private readonly HttpClient _http;
        private readonly FakeLearnerContext _learner;
        private readonly FakeClock _clock;
        private readonly FakeScheduler _scheduler = new();
        private readonly RandomIds _ids = new();

        public Device(HttpClient http, Guid installationId, FakeLearnerContext learner, FakeClock clock)
        {
            _http = http;
            _learner = learner;
            _clock = clock;
            Credentials = new FakeDeviceCredentials(installationId);
            Gateway = new HttpSyncGateway(http, new SyncOptions(new Uri("http://localhost/"), "android", "1.0.0", "uk"), Credentials, clock, NullLogger<HttpSyncGateway>.Instance);
        }

        public FakeDeviceCredentials Credentials { get; }

        public HttpSyncGateway Gateway { get; }

        public InMemoryCardRepository Cards { get; } = new();

        public InMemoryReviewLog Reviews { get; } = new();

        public InMemoryExposureStore Exposures { get; } = new();

        public InMemoryProgressOutbox Outbox { get; } = new();

        public InMemorySyncState State { get; } = new();

        public InMemoryContentRepository Content { get; } = new();

        public InMemorySyncableSettings Settings { get; } = new();

        public GradeAnswerUseCase Grade => new(_learner, Cards, Reviews, _scheduler, _ids, _clock, new LearningDayResolver(_clock), Outbox);

        public SyncUseCase Sync => new(
            Gateway,
            _learner,
            State,
            new SyncProgressUseCase(Gateway, Outbox, State, Credentials, _learner, Cards, Reviews, Exposures, _scheduler, _ids, _clock, NullLogger<SyncProgressUseCase>.Instance),
            new SyncSettingsUseCase(Gateway, Settings, State, Credentials),
            new RefreshContentUseCase(Gateway, Content, _learner, State, NullLogger<RefreshContentUseCase>.Instance),
            _clock,
            NullLogger<SyncUseCase>.Instance);

        public void Dispose()
        {
            Gateway.Dispose();
            _http.Dispose();
        }
    }
}
