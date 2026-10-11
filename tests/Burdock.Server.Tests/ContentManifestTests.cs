using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Content;
using Burdock.Domain.Entitlements;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Content.Application;
using Burdock.Server.Modules.Content.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Tests;

/// <summary>Host with a temp pack store served locally and a configurable default entitlement (spec 09 §3.4).</summary>
public sealed class ContentApiFactory(string connectionString, string defaultState) : BurdockApiFactory(connectionString)
{
    public string StorePath { get; } = Path.Combine(Path.GetTempPath(), "burdock-packs-" + Guid.NewGuid().ToString("N"));

    protected override void Configure(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Content:StorePath", StorePath);
        builder.UseSetting("Content:PublicBaseUrl", "https://cdn.test/");
        builder.UseSetting("Content:ServeStoreLocally", "true");
        builder.UseSetting("Entitlements:DefaultState", defaultState);
    }

    public async Task<PublishedPack> PublishAsync(string packId, string level, string topic, int version, string json)
    {
        using var scope = Services.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<PublishPackUseCase>();
        var identity = new PackIdentity(packId, LanguagePair.UkrainianToEnglish, level, topic, version);
        var result = await useCase.ExecuteAsync(new PublishPackCommand(identity, Encoding.UTF8.GetBytes(json), "1.0.0", [new PackAttribution("Test source", "CC0", null, null)]), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue(result.Error?.Code);
        return result.Value;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(StorePath))
        {
            Directory.Delete(StorePath, recursive: true);
        }
    }
}

[Collection(PostgresBackedApi.Name)]
public sealed class ContentManifestTests(PostgresFixture postgres)
{
    private static int _sequence;

    [Fact]
    public async Task Published_packs_appear_in_the_manifest_with_cdn_urls_and_object_hashes()
    {
        using var factory = new ContentApiFactory(postgres.ConnectionString, "Trial");
        using var client = factory.CreateClient();
        var suffix = NextSuffix();
        var free = await factory.PublishAsync($"uk-en.free-pool-{suffix}", "A1-B1", "free-pool", 1, """{"packId":"free","entries":[]}""");
        var travel = await factory.PublishAsync($"uk-en.a1.travel-{suffix}", "A1", "travel", 1, """{"packId":"travel","entries":[1,2,3]}""");

        var session = await BurdockApiFactory.SignUpAsync(client);
        using var request = BurdockApiFactory.Authorized(HttpMethod.Get, ApiRoutes.Content.Manifest, session.Tokens.AccessToken);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var manifest = (await response.Content.ReadFromJsonAsync<ContentManifestDto>(TestContext.Current.CancellationToken))!;
        var packs = manifest.Packs.Where(p => p.Id.EndsWith(suffix, StringComparison.Ordinal)).ToDictionary(p => p.Id, StringComparer.Ordinal);
        packs.Count.ShouldBe(2);
        packs[free.Pack.PackId].Required.ShouldBeTrue();
        packs[travel.Pack.PackId].Required.ShouldBeFalse();
        packs[travel.Pack.PackId].Url.ShouldBe(new Uri($"https://cdn.test/packs/v1/uk-en/a1/travel/uk-en.a1.travel-{suffix}-1.json.gz"));
        packs[travel.Pack.PackId].Sha256.ShouldBe(travel.Pack.Sha256);
        packs[travel.Pack.PackId].Attributions.Single().Source.ShouldBe("Test source");
        manifest.ManifestVersion.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Free_learners_only_see_the_free_pool_and_get_a_different_etag()
    {
        var suffix = NextSuffix();
        using var trial = new ContentApiFactory(postgres.ConnectionString, "Trial");
        await trial.PublishAsync($"uk-en.free-pool-{suffix}", "A1-B1", "free-pool", 1, """{"packId":"free"}""");
        await trial.PublishAsync($"uk-en.b2.work-{suffix}", "B2", "work", 1, """{"packId":"work"}""");
        using var free = new ContentApiFactory(postgres.ConnectionString, "Free");

        using var trialClient = trial.CreateClient();
        using var freeClient = free.CreateClient();
        var trialSession = await BurdockApiFactory.SignUpAsync(trialClient);
        var freeSession = await BurdockApiFactory.SignUpAsync(freeClient);
        using var trialRequest = BurdockApiFactory.Authorized(HttpMethod.Get, ApiRoutes.Content.Manifest, trialSession.Tokens.AccessToken);
        using var freeRequest = BurdockApiFactory.Authorized(HttpMethod.Get, ApiRoutes.Content.Manifest, freeSession.Tokens.AccessToken);
        using var trialResponse = await trialClient.SendAsync(trialRequest, TestContext.Current.CancellationToken);
        using var freeResponse = await freeClient.SendAsync(freeRequest, TestContext.Current.CancellationToken);

        var trialPacks = (await trialResponse.Content.ReadFromJsonAsync<ContentManifestDto>(TestContext.Current.CancellationToken))!.Packs.Where(p => p.Id.EndsWith(suffix, StringComparison.Ordinal)).ToArray();
        var freePacks = (await freeResponse.Content.ReadFromJsonAsync<ContentManifestDto>(TestContext.Current.CancellationToken))!.Packs;
        trialPacks.Length.ShouldBe(2);
        freePacks.ShouldAllBe(p => p.Topic == ContentPack.FreePoolTopic);
        freePacks.ShouldContain(p => p.Id == $"uk-en.free-pool-{suffix}");
        trialResponse.Headers.ETag!.Tag.ShouldNotBe(freeResponse.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task Republishing_is_idempotent_for_identical_content_and_a_conflict_otherwise()
    {
        using var factory = new ContentApiFactory(postgres.ConnectionString, "Trial");
        var suffix = NextSuffix();
        var first = await factory.PublishAsync($"uk-en.a2.food-{suffix}", "A2", "food", 1, """{"v":1}""");
        var again = await factory.PublishAsync($"uk-en.a2.food-{suffix}", "A2", "food", 1, """{"v":1}""");

        first.AlreadyPublished.ShouldBeFalse();
        again.AlreadyPublished.ShouldBeTrue();
        again.Pack.Sha256.ShouldBe(first.Pack.Sha256);

        using var scope = factory.Services.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<PublishPackUseCase>();
        var identity = new PackIdentity($"uk-en.a2.food-{suffix}", LanguagePair.UkrainianToEnglish, "A2", "food", 1);
        var conflict = await useCase.ExecuteAsync(new PublishPackCommand(identity, Encoding.UTF8.GetBytes("""{"v":2}"""), "1.0.0", []), TestContext.Current.CancellationToken);
        conflict.IsFailure.ShouldBeTrue();
        conflict.Error!.Code.ShouldBe("content.pack_version_conflict");
    }

    [Fact]
    public async Task A_newer_version_replaces_the_older_one_in_the_manifest_and_deprecates_it()
    {
        using var factory = new ContentApiFactory(postgres.ConnectionString, "Trial");
        using var client = factory.CreateClient();
        var suffix = NextSuffix();
        var id = $"uk-en.b1.city-{suffix}";
        await factory.PublishAsync(id, "B1", "city", 1, """{"v":1}""");
        var second = await factory.PublishAsync(id, "B1", "city", 2, """{"v":2}""");

        var session = await BurdockApiFactory.SignUpAsync(client);
        using var request = BurdockApiFactory.Authorized(HttpMethod.Get, ApiRoutes.Content.Manifest, session.Tokens.AccessToken);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var listed = (await response.Content.ReadFromJsonAsync<ContentManifestDto>(TestContext.Current.CancellationToken))!.Packs.Where(p => p.Id == id).ToArray();

        listed.Length.ShouldBe(1);
        listed[0].Version.ShouldBe(2);
        listed[0].Sha256.ShouldBe(second.Pack.Sha256);
        using var scope = factory.Services.CreateScope();
        var older = await scope.ServiceProvider.GetRequiredService<IPackCatalog>().GetAsync(id, 1, TestContext.Current.CancellationToken);
        older!.Status.ShouldBe(PackStatus.Deprecated);
        older.DeprecatedAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_local_store_serves_the_gzipped_object_that_matches_the_recorded_hash()
    {
        using var factory = new ContentApiFactory(postgres.ConnectionString, "Trial");
        using var client = factory.CreateClient();
        var suffix = NextSuffix();
        const string json = """{"packId":"uk-en.c1.it","entries":["cybersecurity"]}""";
        var published = await factory.PublishAsync($"uk-en.c1.it-{suffix}", "C1", "it", 1, json);

        using var response = await client.GetAsync(new Uri(ApiRoutes.Content.Group + "/store/" + published.Pack.ObjectKey, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/gzip");
        response.Headers.CacheControl!.MaxAge.ShouldBe(TimeSpan.FromDays(365));
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Convert.ToHexStringLower(SHA256.HashData(bytes)).ShouldBe(published.Pack.Sha256);
        bytes.Length.ShouldBe((int)published.Pack.SizeBytes);
        using var gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe(json);
    }

    private static string NextSuffix() => Interlocked.Increment(ref _sequence).ToString(System.Globalization.CultureInfo.InvariantCulture) + Guid.NewGuid().ToString("N")[..6];
}

/// <summary>Pure rules of the Content module (spec 09 §3.4).</summary>
public sealed class ContentPackDomainTests
{
    private static readonly PackIdentity Travel = new("uk-en.a1.travel", LanguagePair.UkrainianToEnglish, "A1", "travel", 3);
    private static readonly PackIdentity FreePool = new("uk-en.free-pool", LanguagePair.UkrainianToEnglish, "A1-B1", "free-pool", 1);
    private static readonly string Hash = new('a', 64);

    [Theory]
    [InlineData(EntitlementState.Free, false)]
    [InlineData(EntitlementState.Trial, true)]
    [InlineData(EntitlementState.Active, true)]
    [InlineData(EntitlementState.Grace, true)]
    [InlineData(EntitlementState.BillingRetry, true)]
    [InlineData(EntitlementState.Expired, false)]
    [InlineData(EntitlementState.Revoked, false)]
    public void Topic_packs_follow_the_entitlement_while_the_free_pool_is_always_visible(EntitlementState state, bool expected)
    {
        var travel = ContentPack.Publish(Travel, Hash, 10, "k", "1.0.0", [], DateTimeOffset.UnixEpoch).Value!;
        var free = ContentPack.Publish(FreePool, Hash, 10, "k", "1.0.0", [], DateTimeOffset.UnixEpoch).Value!;

        travel.IsVisibleTo(state).ShouldBe(expected);
        free.IsVisibleTo(state).ShouldBeTrue();
    }

    [Fact]
    public void Deprecated_packs_are_never_visible()
    {
        var travel = ContentPack.Publish(Travel, Hash, 10, "k", "1.0.0", [], DateTimeOffset.UnixEpoch).Value!;
        travel.Deprecate(new DateOnly(2027, 1, 1));

        travel.IsVisibleTo(EntitlementState.Active).ShouldBeFalse();
        travel.Status.ShouldBe(PackStatus.Deprecated);
    }

    [Fact]
    public void Object_keys_are_versioned_and_unlisted()
        => ContentPack.ObjectKeyFor(Travel).ShouldBe("packs/v1/uk-en/a1/travel/uk-en.a1.travel-3.json.gz");

    [Theory]
    [InlineData("en.a1.travel", "A1", "travel", 1)]
    [InlineData("uk-en.A1.travel", "A1", "travel", 1)]
    [InlineData("uk-en.a1.travel", "A1", "Travel", 1)]
    [InlineData("uk-en.a1.travel", "A1", "travel", 0)]
    public void Invalid_identities_are_rejected(string id, string level, string topic, int version)
    {
        var result = ContentPack.Publish(new PackIdentity(id, LanguagePair.UkrainianToEnglish, level, topic, version), Hash, 10, "k", "1.0.0", [], DateTimeOffset.UnixEpoch);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("content.pack_identity_invalid");
    }

    [Fact]
    public void Gzip_is_deterministic_so_identical_json_yields_identical_hashes()
    {
        var json = Encoding.UTF8.GetBytes("""{"a":1}""");

        PublishPackUseCase.Compress(json).ShouldBe(PublishPackUseCase.Compress(json));
    }
}
