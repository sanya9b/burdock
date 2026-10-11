using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Sessions;
using Burdock.Application.UseCases.Sync;
using Burdock.Application.UseCases.Words;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Sync;
using Burdock.Domain.Vocabulary;
using Burdock.TestKit;
using Burdock.TestKit.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Burdock.Application.Tests.UseCases;

/// <summary>Client side of the progress, settings and content sync (spec 09 sections 3.4-3.6, T14) over an in-memory server.</summary>
public sealed class SyncTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("0199a4c0-0000-7000-8000-00000000bbbb"));
    private static readonly Guid InstallationA = Guid.Parse("0199a4c0-0000-7000-8000-0000000000a1");
    private static readonly Guid InstallationB = Guid.Parse("0199a4c0-0000-7000-8000-0000000000b2");
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly WordEntry Journey = Entry("journey", "подорож", "0199a500-0000-7000-8000-000000000101");

    private readonly FakeClock _clock = new(Now, TestTimeZones.KyivFixed);
    private readonly FakeSyncGateway _gateway = new();
    private readonly FakeLearnerContext _learner = new(Learner, LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, []));

    [Fact]
    public async Task Grading_queues_a_ReviewLog_event_whose_id_is_the_log_entry_id()
    {
        var device = Device(InstallationA);

        var graded = await device.Grade.ExecuteAsync(Choice(Journey), new ExerciseOutcome(ExerciseMode.ChoiceTargetToNative, Correct: true, TimeSpan.FromSeconds(2)), Ct);

        graded.IsSuccess.ShouldBeTrue();
        var queued = device.Outbox.Events.ShouldHaveSingleItem();
        queued.Type.ShouldBe(ProgressEventTypes.ReviewLog);
        queued.Id.ShouldBe(device.Reviews.Entries.Single().Id);
        queued.OccurredAt.ShouldBe(Now);
        queued.LocalDate.ShouldBe(new DateOnly(2026, 10, 11));
        queued.InstallationId.ShouldBeNull();
        queued.PayloadJson.ShouldContain(Journey.Id.Value.ToString("D"));
        queued.PayloadJson.ShouldContain("\"mode\":\"ChoiceTargetToNative\"");
    }

    [Fact]
    public async Task Opening_a_word_queues_ExposureConfirmed_but_a_scheduled_exposure_does_not()
    {
        var device = Device(InstallationA);

        await device.Exposure.ExecuteAsync(Journey.Id, ExposureSurface.InApp, ExposureKind.Confirmed, Ct);
        await device.Exposure.ExecuteAsync(Journey.Id, ExposureSurface.InApp, ExposureKind.Scheduled, Ct);

        var queued = device.Outbox.Events.ShouldHaveSingleItem();
        queued.Type.ShouldBe(ProgressEventTypes.ExposureConfirmed);
        queued.Id.ShouldBe(device.Exposures.Records.First(r => r.Kind == ExposureKind.Confirmed).Id);
    }

    [Fact]
    public async Task Introducing_a_word_queues_CardIntroduced_only_the_first_time()
    {
        var device = Device(InstallationA);

        await device.Introduce.ExecuteAsync(Journey.Id, Ct);
        await device.Introduce.ExecuteAsync(Journey.Id, Ct);

        device.Outbox.Events.ShouldHaveSingleItem().Type.ShouldBe(ProgressEventTypes.CardIntroduced);
    }

    [Fact]
    public async Task Progress_made_on_one_device_replays_on_the_other_and_replays_are_idempotent()
    {
        var a = Device(InstallationA);
        var b = Device(InstallationB);
        await a.Grade.ExecuteAsync(Choice(Journey), new ExerciseOutcome(ExerciseMode.ChoiceTargetToNative, Correct: true, TimeSpan.FromSeconds(2)), Ct);

        var firstOnA = (await a.Progress.ExecuteAsync(Ct)).Value;
        var firstOnB = (await b.Progress.ExecuteAsync(Ct)).Value;
        var secondOnB = (await b.Progress.ExecuteAsync(Ct)).Value;
        var secondOnA = (await a.Progress.ExecuteAsync(Ct)).Value;

        firstOnA.ShouldBe(new ProgressSyncReport(Pushed: 1, Rejected: 0, Pulled: 1, Applied: 0, Cursor: 1));
        firstOnB.ShouldBe(new ProgressSyncReport(0, 0, 1, 1, 1));
        secondOnB.Pulled.ShouldBe(0);
        secondOnA.Pulled.ShouldBe(0);
        a.Outbox.Events.ShouldBeEmpty();
        var cardOnB = (await b.Cards.GetByLexemeAsync(Learner, Journey.Id, Ct)).ShouldNotBeNull();
        var cardOnA = (await a.Cards.GetByLexemeAsync(Learner, Journey.Id, Ct)).ShouldNotBeNull();
        cardOnB.Id.ShouldBe(cardOnA.Id);
        cardOnB.State.ShouldBe(cardOnA.State);
        cardOnB.Reps.ShouldBe(1);
        cardOnB.Due.ShouldBe(cardOnA.Due);
        b.Reviews.Entries.ShouldHaveSingleItem().Id.ShouldBe(a.Reviews.Entries.Single().Id);
        b.Outbox.Events.ShouldBeEmpty();

        await b.Grade.ExecuteAsync(Choice(Journey), new ExerciseOutcome(ExerciseMode.ChoiceTargetToNative, Correct: true, TimeSpan.FromSeconds(1)), Ct);
        (await b.Progress.ExecuteAsync(Ct)).Value.Pushed.ShouldBe(1);
        var thirdOnA = (await a.Progress.ExecuteAsync(Ct)).Value;

        thirdOnA.Applied.ShouldBe(1);
        (await a.Cards.GetByLexemeAsync(Learner, Journey.Id, Ct))!.Reps.ShouldBe(2);
        a.Reviews.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task An_offline_push_keeps_the_outbox_and_reports_sync_offline()
    {
        var device = Device(InstallationA);
        await device.Grade.ExecuteAsync(Choice(Journey), new ExerciseOutcome(ExerciseMode.ChoiceTargetToNative, Correct: false, TimeSpan.FromSeconds(2)), Ct);
        _gateway.Offline = true;

        var offline = await device.Progress.ExecuteAsync(Ct);
        _gateway.Offline = false;
        var online = await device.Progress.ExecuteAsync(Ct);

        offline.IsFailure.ShouldBeTrue();
        offline.Error!.Code.ShouldBe("sync.offline");
        online.IsSuccess.ShouldBeTrue();
        online.Value.Pushed.ShouldBe(1);
        device.Outbox.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Settings_merge_both_ways_with_last_writer_wins()
    {
        var a = Device(InstallationA);
        var b = Device(InstallationB);
        a.Settings.SetLocal("level", "\"B1\"", Now);
        b.Settings.SetLocal("level", "\"A2\"", Now.AddMinutes(1));
        b.Settings.SetLocal("gamification", "false", Now);

        var firstOnA = (await a.SettingsSync.ExecuteAsync(Ct)).Value;
        var firstOnB = (await b.SettingsSync.ExecuteAsync(Ct)).Value;
        var secondOnA = (await a.SettingsSync.ExecuteAsync(Ct)).Value;

        firstOnA.ShouldBe(new SettingsSyncReport(AppliedFromServer: 0, SentToServer: 1));
        firstOnB.ShouldBe(new SettingsSyncReport(0, 2));
        secondOnA.AppliedFromServer.ShouldBe(2);
        a.Settings.Entries["level"].ValueJson.ShouldBe("\"A2\"");
        a.Settings.Entries["level"].ClientId.ShouldBe(InstallationB.ToString("N"));
        a.Settings.Entries["gamification"].ValueJson.ShouldBe("false");
        _gateway.Settings["level"].ValueJson.ShouldBe("\"A2\"");
        a.State.Values[SyncSettingsUseCase.EtagKey].ShouldBe(SettingsDocumentRules.ComputeEtag(_gateway.Settings.Values));
    }

    [Fact]
    public async Task Content_refresh_imports_newer_packs_only_and_honours_the_manifest_etag()
    {
        var device = Device(InstallationA);
        var travel = new RemotePack("uk-en.a2.travel", "A2", "travel", 1, "00", 10, new Uri("https://cdn.example/packs/travel-1.json.gz"), Required: false);
        _gateway.Publish(travel, new ImportedContentPack(travel.Id, LanguagePair.UkrainianToEnglish, 1, [Journey]));

        var first = (await device.Refresh.ExecuteAsync(Ct)).Value;
        var second = (await device.Refresh.ExecuteAsync(Ct)).Value;
        _gateway.Publish(travel with { Version = 2 }, new ImportedContentPack(travel.Id, LanguagePair.UkrainianToEnglish, 2, [Journey, Entry("ticket", "квиток", "0199a500-0000-7000-8000-000000000102")]));
        _gateway.ManifestEtag = "manifest-2";
        var third = (await device.Refresh.ExecuteAsync(Ct)).Value;

        first.ManifestChanged.ShouldBeTrue();
        first.PacksImported.ShouldBe(1);
        first.Failed.ShouldBeEmpty();
        second.ManifestChanged.ShouldBeFalse();
        third.PacksImported.ShouldBe(1);
        _gateway.DownloadCalls.ShouldBe(2);
        (await device.Content.GetImportedPackVersionsAsync(Ct))[travel.Id].ShouldBe(2);
        (await device.Content.GetAsync(Journey.Id, Ct)).ShouldNotBeNull();
        device.State.Values[RefreshContentUseCase.EtagKey].ShouldBe("manifest-2");
    }

    [Fact]
    public async Task A_full_pass_stops_without_an_account_and_otherwise_records_the_account_and_the_time()
    {
        var device = Device(InstallationA);
        _gateway.TakenLearnerIds.Add(Learner.Value);

        var refused = await device.All.ExecuteAsync(Ct);
        _gateway.TakenLearnerIds.Clear();
        var synced = await device.All.ExecuteAsync(Ct);

        refused.Online.ShouldBeFalse();
        refused.Error.ShouldBe("sync.learner_id_taken");
        synced.Online.ShouldBeTrue();
        synced.Error.ShouldBeNull();
        synced.Progress.ShouldNotBeNull();
        synced.Settings.ShouldNotBeNull();
        synced.Content.ShouldNotBeNull();
        device.State.Values[SyncUseCase.AccountKey].ShouldBe(Learner.Value.ToString("D"));
        device.State.Values[SyncUseCase.LastSyncKey].ShouldBe(Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static WordEntry Entry(string lemma, string translation, string id)
        => new(LexemeBuilder.ALexeme().WithLemma(lemma).AtLevel(CefrLevel.A2).InTopics("travel").WithId(Guid.Parse(id)).Build(), translation, [], [lemma], null, null, null, null);

    private static ChoiceItem Choice(WordEntry entry) => new(entry, ExerciseMode.ChoiceTargetToNative, entry.Lexeme.Lemma, [entry.Translation, "x", "y", "z"], 0);

    private TestDevice Device(Guid installation) => new(installation, _gateway, _learner, _clock);

    private sealed class RandomIds : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    /// <summary>One installation: its own local stores over the shared gateway, learner and clock.</summary>
    private sealed class TestDevice(Guid installation, FakeSyncGateway gateway, FakeLearnerContext learner, FakeClock clock)
    {
        private readonly FakeScheduler _scheduler = new();
        private readonly RandomIds _ids = new();

        public InMemoryCardRepository Cards { get; } = new();

        public InMemoryReviewLog Reviews { get; } = new();

        public InMemoryExposureStore Exposures { get; } = new();

        public InMemoryProgressOutbox Outbox { get; } = new();

        public InMemorySyncState State { get; } = new();

        public InMemoryContentRepository Content { get; } = new();

        public InMemorySyncableSettings Settings { get; } = new();

        public FakeDeviceCredentials Credentials { get; } = new(installation);

        public GradeAnswerUseCase Grade => new(learner, Cards, Reviews, _scheduler, _ids, clock, new LearningDayResolver(clock), Outbox);

        public RecordExposureUseCase Exposure => new(learner, Cards, Exposures, _ids, clock, new LearningDayResolver(clock), Outbox);

        public IntroduceWordUseCase Introduce => new(learner, Cards, _ids, clock, Outbox);

        public SyncProgressUseCase Progress => new(gateway, Outbox, State, Credentials, learner, Cards, Reviews, Exposures, _scheduler, _ids, clock, NullLogger<SyncProgressUseCase>.Instance);

        public SyncSettingsUseCase SettingsSync => new(gateway, Settings, State, Credentials);

        public RefreshContentUseCase Refresh => new(gateway, Content, learner, State, NullLogger<RefreshContentUseCase>.Instance);

        public SyncUseCase All => new(gateway, learner, State, Progress, SettingsSync, Refresh, clock, NullLogger<SyncUseCase>.Instance);
    }
}
