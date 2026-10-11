using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Progress;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Sync;
using Burdock.Server.Modules.LearnerProgress.Domain;

namespace Burdock.Server.Tests;

/// <summary>Append-only progress sync (spec 09 §3.5, FR-09-10/11) against PostgreSQL.</summary>
[Collection(PostgresBackedApi.Name)]
public sealed class ProgressEventsTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private readonly BurdockApiFactory _factory;
    private readonly HttpClient _client;

    public ProgressEventsTests(PostgresFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        _factory = new BurdockApiFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task A_batch_is_accepted_once_and_reported_as_duplicates_afterwards()
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);
        var events = new[] { Event(ProgressEventTypes.ReviewLog, Now.AddMinutes(-5)), Event(ProgressEventTypes.ExposureDaily, Now.AddMinutes(-4)) };

        var first = await PostAsync(session, events);
        var second = await PostAsync(session, events);

        first.Accepted.ShouldBe(events.Select(e => e.Id));
        first.Duplicates.ShouldBeEmpty();
        first.Rejected.ShouldBeEmpty();
        first.ServerSeq.ShouldBe(2);
        second.Accepted.ShouldBeEmpty();
        second.Duplicates.ShouldBe(events.Select(e => e.Id));
        second.ServerSeq.ShouldBe(2);
    }

    [Fact]
    public async Task Invalid_envelopes_are_rejected_individually_with_a_code()
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);
        var good = Event(ProgressEventTypes.CardIntroduced, Now.AddMinutes(-1));
        var future = Event(ProgressEventTypes.ReviewLog, Now.AddHours(1));
        var unknown = Event("SomethingElse", Now.AddMinutes(-1));
        var farDate = Event(ProgressEventTypes.ReviewLog, Now.AddMinutes(-1)) with { LocalDate = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-3) };

        var result = await PostAsync(session, [good, future, unknown, farDate]);

        result.Accepted.ShouldBe([good.Id]);
        result.Rejected.Select(r => (r.Id, r.Code)).ShouldBe([
            (future.Id, ProgressRejectionCodes.FutureTimestamp),
            (unknown.Id, ProgressRejectionCodes.UnknownType),
            (farDate.Id, ProgressRejectionCodes.LocalDateImplausible),
        ]);
        result.ServerSeq.ShouldBe(1);
    }

    [Fact]
    public async Task Events_are_read_back_in_sequence_order_with_paging_and_installation_exclusion()
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        var batch = Enumerable.Range(0, 5).Select(i => Event(ProgressEventTypes.ReviewLog, Now.AddMinutes(-10 + i), i % 2 == 0 ? mine : other)).ToArray();
        await PostAsync(session, batch);

        var firstPage = await ReadAsync(session, since: 0, limit: 2);
        var secondPage = await ReadAsync(session, since: firstPage.NextSince, limit: 2);
        var rest = await ReadAsync(session, since: secondPage.NextSince, limit: 2);
        var others = await ReadAsync(session, since: 0, limit: 10, exclude: mine);

        firstPage.Events.Select(e => e.ServerSeq).ShouldBe([1L, 2L]);
        firstPage.HasMore.ShouldBeTrue();
        secondPage.Events.Select(e => e.ServerSeq).ShouldBe([3L, 4L]);
        rest.Events.Select(e => e.ServerSeq).ShouldBe([5L]);
        rest.HasMore.ShouldBeFalse();
        rest.NextSince.ShouldBe(5);
        firstPage.Events[0].Event.Payload.GetProperty("rating").GetInt32().ShouldBe(3);
        others.Events.Select(e => e.Event.InstallationId).ShouldAllBe(id => id == other);
        others.Events.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Learners_never_see_each_others_events()
    {
        var alice = await BurdockApiFactory.SignUpAsync(_client);
        var bob = await BurdockApiFactory.SignUpAsync(_client, "android");
        await PostAsync(alice, [Event(ProgressEventTypes.ReviewLog, Now.AddMinutes(-1))]);

        var bobsView = await ReadAsync(bob, since: 0, limit: 10);
        var bobsAppend = await PostAsync(bob, [Event(ProgressEventTypes.ReviewLog, Now.AddMinutes(-1))]);

        bobsView.Events.ShouldBeEmpty();
        bobsAppend.ServerSeq.ShouldBe(1);
    }

    [Fact]
    public async Task An_empty_batch_returns_the_current_cursor_and_the_endpoints_require_a_token()
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);
        var empty = await PostAsync(session, []);
        empty.ServerSeq.ShouldBe(0);

        using var anonymous = await _client.GetAsync(new Uri(ApiRoutes.Progress.Events + "?since=0", UriKind.Relative), TestContext.Current.CancellationToken);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static ProgressEventDto Event(string type, DateTimeOffset occurredAt, Guid? installation = null)
        => new(Guid.CreateVersion7(), type, 1, occurredAt, DateOnly.FromDateTime(occurredAt.UtcDateTime), installation, JsonSerializer.SerializeToElement(new { lexemeId = Guid.NewGuid(), rating = 3 }));

    private async Task<PostEventsResultDto> PostAsync(TestSession session, IReadOnlyList<ProgressEventDto> events)
    {
        using var request = BurdockApiFactory.Authorized(HttpMethod.Post, ApiRoutes.Progress.Events, session.Tokens.AccessToken);
        request.Content = JsonContent.Create(new PostEventsDto(events));
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<PostEventsResultDto>(body, JsonSerializerOptions.Web)!;
    }

    private async Task<EventsPageDto> ReadAsync(TestSession session, long since, int limit, Guid? exclude = null)
    {
        var path = $"{ApiRoutes.Progress.Events}?since={since}&limit={limit}" + (exclude is null ? string.Empty : $"&excludeInstallation={exclude:D}");
        using var request = BurdockApiFactory.Authorized(HttpMethod.Get, path, session.Tokens.AccessToken);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<EventsPageDto>(body, JsonSerializerOptions.Web)!;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

/// <summary>Envelope rules of the progress module (spec 09 §3.5).</summary>
public sealed class ProgressEventDomainTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(5);

    [Fact]
    public void A_well_formed_envelope_is_accepted()
    {
        var result = ProgressEvent.Accept(Guid.NewGuid(), Learner, ProgressEventTypes.ReviewLog, 1, Now.AddMinutes(-1), new DateOnly(2026, 10, 11), null, """{"rating":3}""", Now, Skew);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ServerSeq.ShouldBeNull();
    }

    [Theory]
    [InlineData("ReviewLog", 0, 4, "schema_invalid")]
    [InlineData("Nope", 1, 4, "unknown_type")]
    [InlineData("ReviewLog", 1, -30, "future_timestamp")]
    public void Bad_envelopes_carry_the_rejection_code(string type, int schemaVersion, int minutesAgo, string code)
    {
        var result = ProgressEvent.Accept(Guid.NewGuid(), Learner, type, schemaVersion, Now.AddMinutes(-minutesAgo), new DateOnly(2026, 10, 11), null, "{}", Now, Skew);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(code);
    }

    [Fact]
    public void A_local_date_more_than_a_day_away_is_implausible_but_a_neighbouring_day_is_fine()
    {
        ProgressEvent.Accept(Guid.NewGuid(), Learner, ProgressEventTypes.ExposureDaily, 1, Now, new DateOnly(2026, 10, 12), null, "{}", Now, Skew).IsSuccess.ShouldBeTrue();
        ProgressEvent.Accept(Guid.NewGuid(), Learner, ProgressEventTypes.ExposureDaily, 1, Now, new DateOnly(2026, 10, 9), null, "{}", Now, Skew).Error!.Code.ShouldBe("local_date_implausible");
    }

    [Fact]
    public void Oversized_payloads_are_rejected()
    {
        var payload = "{\"blob\":\"" + new string('x', ProgressEvent.MaxPayloadBytes) + "\"}";

        ProgressEvent.Accept(Guid.NewGuid(), Learner, ProgressEventTypes.ReviewLog, 1, Now, new DateOnly(2026, 10, 11), null, payload, Now, Skew).Error!.Code.ShouldBe("payload_too_large");
    }
}
