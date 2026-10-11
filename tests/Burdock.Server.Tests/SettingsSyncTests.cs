using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Settings;
using Burdock.Domain.Sync;

namespace Burdock.Server.Tests;

/// <summary>Last-writer-wins settings sync (spec 09 §3.6, FR-09-13) against PostgreSQL.</summary>
[Collection(PostgresBackedApi.Name)]
public sealed class SettingsSyncTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 10, 0, 0, TimeSpan.Zero);
    private readonly BurdockApiFactory _factory;
    private readonly HttpClient _client;

    public SettingsSyncTests(PostgresFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        _factory = new BurdockApiFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task A_fresh_learner_has_an_empty_document_and_put_then_get_round_trips()
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);

        var (emptyStatus, empty, emptyEtag) = await GetAsync(session);
        emptyStatus.ShouldBe(HttpStatusCode.OK);
        empty!.Entries.ShouldBeEmpty();
        empty.SchemaVersion.ShouldBe(1);

        var put = await PutAsync(session, Entries(("newWordsPerDay", 10, T0, "phone-a"), ("theme", "dark", T0, "phone-a")));
        put.Entries.Count.ShouldBe(2);
        put.Entries["newWordsPerDay"].Value.GetInt32().ShouldBe(10);
        put.Etag.ShouldNotBe(empty.Etag);

        var (status, fetched, etag) = await GetAsync(session);
        status.ShouldBe(HttpStatusCode.OK);
        fetched!.Entries["theme"].Value.GetString().ShouldBe("dark");
        fetched.Etag.ShouldBe(put.Etag);
        etag.ShouldBe($"\"{put.Etag}\"");

        var (notModified, _, _) = await GetAsync(session, ifNoneMatch: etag);
        notModified.ShouldBe(HttpStatusCode.NotModified);
        emptyEtag.ShouldNotBeNull();
    }

    [Fact]
    public async Task Merging_keeps_the_newer_stamp_per_key_and_breaks_ties_by_client_id()
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);
        await PutAsync(session, Entries(("newWordsPerDay", 10, T0.AddMinutes(5), "phone-b"), ("theme", "dark", T0.AddMinutes(5), "phone-b"), ("quietStart", "22:00", T0.AddMinutes(5), "phone-b")));

        var merged = await PutAsync(session, Entries(
            ("newWordsPerDay", 20, T0.AddMinutes(1), "phone-a"),   // older: loses
            ("theme", "light", T0.AddMinutes(9), "phone-a"),       // newer: wins
            ("quietStart", "23:00", T0.AddMinutes(5), "phone-a")));  // tie: "phone-b" > "phone-a", so the stored one stays

        merged.Entries["newWordsPerDay"].Value.GetInt32().ShouldBe(10);
        merged.Entries["newWordsPerDay"].ClientId.ShouldBe("phone-b");
        merged.Entries["theme"].Value.GetString().ShouldBe("light");
        merged.Entries["theme"].ClientId.ShouldBe("phone-a");
        merged.Entries["quietStart"].Value.GetString().ShouldBe("22:00");

        var tieWinner = await PutAsync(session, Entries(("quietStart", "21:00", T0.AddMinutes(5), "phone-z")));
        tieWinner.Entries["quietStart"].Value.GetString().ShouldBe("21:00");
    }

    [Theory]
    [InlineData("bad key!", "settings.key_invalid")]
    [InlineData("", "settings.key_invalid")]
    public async Task Invalid_keys_are_rejected(string key, string code)
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);
        using var request = BurdockApiFactory.Authorized(HttpMethod.Put, ApiRoutes.Settings.Document, session.Tokens.AccessToken);
        request.Content = JsonContent.Create(new PutSettingsDto(1, new Dictionary<string, SettingsEntryDto>(StringComparer.Ordinal) { [key] = new(JsonSerializer.SerializeToElement(1), T0, "phone-a") }));

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain(code);
    }

    [Fact]
    public async Task A_newer_schema_version_is_refused_and_learners_are_isolated()
    {
        var alice = await BurdockApiFactory.SignUpAsync(_client);
        var bob = await BurdockApiFactory.SignUpAsync(_client, "android");
        await PutAsync(alice, Entries(("theme", "dark", T0, "phone-a")));

        using var request = BurdockApiFactory.Authorized(HttpMethod.Put, ApiRoutes.Settings.Document, alice.Tokens.AccessToken);
        request.Content = JsonContent.Create(new PutSettingsDto(2, new Dictionary<string, SettingsEntryDto>(StringComparer.Ordinal)));
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("settings.schema_unsupported");

        var (_, bobs, _) = await GetAsync(bob);
        bobs!.Entries.ShouldBeEmpty();
        using var anonymous = await _client.GetAsync(new Uri(ApiRoutes.Settings.Document, UriKind.Relative), TestContext.Current.CancellationToken);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static Dictionary<string, SettingsEntryDto> Entries(params (string Key, object Value, DateTimeOffset UpdatedAt, string ClientId)[] items)
        => items.ToDictionary(i => i.Key, i => new SettingsEntryDto(JsonSerializer.SerializeToElement(i.Value), i.UpdatedAt, i.ClientId), StringComparer.Ordinal);

    private async Task<SettingsDocumentDto> PutAsync(TestSession session, Dictionary<string, SettingsEntryDto> entries)
    {
        using var request = BurdockApiFactory.Authorized(HttpMethod.Put, ApiRoutes.Settings.Document, session.Tokens.AccessToken);
        request.Content = JsonContent.Create(new PutSettingsDto(1, entries));
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<SettingsDocumentDto>(body, JsonSerializerOptions.Web)!;
    }

    private async Task<(HttpStatusCode Status, SettingsDocumentDto? Document, string? Etag)> GetAsync(TestSession session, string? ifNoneMatch = null)
    {
        using var request = BurdockApiFactory.Authorized(HttpMethod.Get, ApiRoutes.Settings.Document, session.Tokens.AccessToken);
        if (ifNoneMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var etag = response.Headers.ETag?.ToString();
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return (response.StatusCode, null, etag);
        }

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, JsonSerializer.Deserialize<SettingsDocumentDto>(body, JsonSerializerOptions.Web), etag);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

/// <summary>Pure LWW rules (spec 09 §3.6): identical on the client and the server.</summary>
public sealed class SettingsDomainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_later_stamp_wins_and_ties_go_to_the_larger_client_id()
    {
        var older = new SettingsEntry("theme", "\"dark\"", T0, "phone-b");
        var newer = new SettingsEntry("theme", "\"light\"", T0.AddSeconds(1), "phone-a");
        var tie = new SettingsEntry("theme", "\"sepia\"", T0, "phone-c");

        newer.Supersedes(older).ShouldBeTrue();
        older.Supersedes(newer).ShouldBeFalse();
        tie.Supersedes(older).ShouldBeTrue();
        older.Supersedes(tie).ShouldBeFalse();
        older.Supersedes(older).ShouldBeFalse();
    }

    [Fact]
    public void Merge_reports_only_the_entries_that_changed_and_the_etag_is_order_independent()
    {
        var current = new Dictionary<string, SettingsEntry>(StringComparer.Ordinal)
        {
            ["a"] = new("a", "1", T0, "x"),
            ["b"] = new("b", "2", T0, "x"),
        };

        var (merged, changed) = SettingsDocumentRules.Merge(current, [new("a", "9", T0.AddMinutes(-1), "y"), new("b", "8", T0.AddMinutes(1), "y"), new("c", "7", T0, "y")]);

        merged["a"].ValueJson.ShouldBe("1");
        merged["b"].ValueJson.ShouldBe("8");
        merged["c"].ValueJson.ShouldBe("7");
        changed.Select(e => e.Key).ShouldBe(["b", "c"]);
        SettingsDocumentRules.ComputeEtag(merged.Values).ShouldBe(SettingsDocumentRules.ComputeEtag(merged.Values.Reverse()));
        SettingsDocumentRules.ComputeEtag(merged.Values).ShouldNotBe(SettingsDocumentRules.ComputeEtag(current.Values));
    }

    [Theory]
    [InlineData("settings.newWordsPerDay", true)]
    [InlineData("quiet-hours_start", true)]
    [InlineData("has space", false)]
    [InlineData("", false)]
    public void Keys_are_dotted_ascii_identifiers(string key, bool valid)
        => SettingsEntry.Create(key, "1", T0, "phone").IsSuccess.ShouldBe(valid);

    [Fact]
    public void Oversized_values_and_blank_client_ids_are_rejected()
    {
        SettingsEntry.Create("k", "\"" + new string('x', SettingsEntry.MaxValueBytes) + "\"", T0, "phone").Error!.Code.ShouldBe("settings.value_invalid");
        SettingsEntry.Create("k", "1", T0, " ").Error!.Code.ShouldBe("settings.client_invalid");
    }
}
