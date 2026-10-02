using System.Security.Claims;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Identity;
using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Identity.Application;
using Burdock.Server.Modules.Identity.Domain;
using Burdock.Server.Shared.Errors;
using Burdock.Server.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Burdock.Server.Modules.Identity.Endpoints;

/// <summary>POST /v1/identity/anonymous, GET and PATCH /v1/identity/me (spec 09 §3.2.1, §3.3.1).</summary>
public static class IdentityEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(ApiRoutes.Identity.Group).WithTags("Identity");

        group.MapPost("/anonymous", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting(Policies.RateLimits.IdentityCreate)
            .WithName("RegisterAnonymousAccount")
            .WithSummary("Creates the anonymous account of a new installation, or returns the existing one for a known id and secret.");

        group.MapGet("/me", GetMeAsync)
            .RequireAuthorization(Policies.User)
            .RequireRateLimiting(Policies.RateLimits.User)
            .WithName("GetMyAccount");

        group.MapPatch("/me", RenameAsync)
            .RequireAuthorization(Policies.User)
            .RequireRateLimiting(Policies.RateLimits.UserWrite)
            .WithName("RenameMyAccount");
    }

    private static async Task<IResult> RegisterAsync(RegisterInstallationDto body, RegisterInstallationUseCase useCase, CancellationToken cancellationToken)
    {
        if (body is null)
        {
            return DomainError.Validation("identity.body_missing", "Errors_Identity_BodyMissing").ToProblem();
        }

        var result = await useCase.ExecuteAsync(new RegisterInstallationCommand(body.InstallationId, body.InstallationSecret, body.Platform, body.AppVersion), cancellationToken).ConfigureAwait(false);
        return result.ToResult(registered => registered.Created
            ? Results.Created(ApiRoutes.Identity.Me, ToDto(registered.Account))
            : Results.Ok(ToDto(registered.Account)));
    }

    private static async Task<IResult> GetMeAsync(ClaimsPrincipal principal, GetAccountUseCase useCase, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetLearnerId(principal, out var learnerId))
        {
            return Results.Unauthorized();
        }

        var result = await useCase.ExecuteAsync(learnerId, cancellationToken).ConfigureAwait(false);
        return result.ToResult(account => Results.Ok(ToDto(account)));
    }

    private static async Task<IResult> RenameAsync(ClaimsPrincipal principal, RenameAccountDto body, RenameAccountUseCase useCase, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetLearnerId(principal, out var learnerId))
        {
            return Results.Unauthorized();
        }

        var result = await useCase.ExecuteAsync(learnerId, body?.Nickname, cancellationToken).ConfigureAwait(false);
        return result.ToResult(account => Results.Ok(ToDto(account)));
    }

    internal static AccountDto ToDto(UserAccount account) => new(account.Id.Value, account.Nickname, account.Status.ToString(), account.CreatedAt);
}
