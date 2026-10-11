using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Identity;

namespace Burdock.Server.Tests;

/// <summary>A fresh installation may propose the learner id it already uses locally (ADR 0006); the id is kept when free and refused when taken.</summary>
public sealed partial class IdentityEndpointsTests
{
    [Fact]
    public async Task A_proposed_learner_id_becomes_the_account_id_once_and_is_refused_for_another_installation()
    {
        var learnerId = Guid.NewGuid();
        var first = new RegisterInstallationDto(Guid.NewGuid(), Secret(), "android", "1.0.0", "uk", learnerId);
        var second = new RegisterInstallationDto(Guid.NewGuid(), Secret(), "ios", "1.0.0", "uk", learnerId);

        using var created = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Identity.Anonymous, UriKind.Relative), first, TestContext.Current.CancellationToken);
        using var repeated = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Identity.Anonymous, UriKind.Relative), first, TestContext.Current.CancellationToken);
        using var refused = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Identity.Anonymous, UriKind.Relative), second, TestContext.Current.CancellationToken);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await created.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken))!.UserId.ShouldBe(learnerId);
        repeated.StatusCode.ShouldBe(HttpStatusCode.OK);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("learner_id_taken");
    }

    [Fact]
    public async Task An_empty_proposed_learner_id_is_treated_as_none()
    {
        var body = new RegisterInstallationDto(Guid.NewGuid(), Secret(), "android", "1.0.0", null, Guid.Empty);

        using var response = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Identity.Anonymous, UriKind.Relative), body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken))!.UserId.ShouldNotBe(Guid.Empty);
    }

    private static string Secret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
