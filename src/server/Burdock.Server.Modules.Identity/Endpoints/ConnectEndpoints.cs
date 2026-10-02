using System.Security.Claims;
using Burdock.Contracts.V1;
using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Identity.Application;
using Burdock.Server.Shared.Security;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Burdock.Server.Modules.Identity.Endpoints;

/// <summary>
/// POST /v1/connect/token behind OpenIddict's pass-through (spec 09 §3.3.1): the installation grant turns an id + secret
/// into tokens for the anonymous account, the refresh grant re-issues them while the account is active. Revocation is
/// handled by OpenIddict itself.
/// </summary>
public static class ConnectEndpoints
{
    public const string InstallationClaim = "installation";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapPost(ApiRoutes.Connect.Token, ExchangeAsync)
            .AllowAnonymous()
            .RequireRateLimiting(Policies.RateLimits.Token)
            .WithTags("Identity")
            .WithName("ExchangeToken")
            .WithSummary("OpenIddict token endpoint: installation grant and refresh_token grant.")
            .ExcludeFromDescription();
    }

    private static async Task<IResult> ExchangeAsync(HttpContext context, AuthenticateInstallationUseCase installations, GetAccountUseCase accounts, CancellationToken cancellationToken)
    {
        var request = context.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        if (string.Equals(request.GrantType, ApiRoutes.Connect.InstallationGrantType, StringComparison.Ordinal))
        {
            _ = Guid.TryParse((string?)request.GetParameter("installation_id"), out var installationId);
            var result = await installations.ExecuteAsync(installationId, (string?)request.GetParameter("installation_secret"), (string?)request.GetParameter("app_version"), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return Forbid(Errors.InvalidGrant, result.Error.Code);
            }

            var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
            identity.SetClaim(Claims.Subject, result.Value.Id.Value.ToString("D"));
            identity.SetClaim(InstallationClaim, installationId.ToString("D"));
            identity.SetScopes(Scopes.OfflineAccess);
            identity.SetDestinations(static _ => [Destinations.AccessToken]);
            return Results.SignIn(new ClaimsPrincipal(identity), properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsRefreshTokenGrantType())
        {
            var authenticated = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);
            if (authenticated.Principal is not { } principal || !CurrentUser.TryGetLearnerId(principal, out var learnerId))
            {
                return Forbid(Errors.InvalidGrant, "refresh_token_invalid");
            }

            var account = await accounts.ExecuteAsync(learnerId, cancellationToken).ConfigureAwait(false);
            if (account.IsFailure || !account.Value.IsActive)
            {
                return Forbid(Errors.InvalidGrant, "account_unavailable");
            }

            principal.SetDestinations(static _ => [Destinations.AccessToken]);
            return Results.SignIn(principal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Forbid(Errors.UnsupportedGrantType, "grant_type_unsupported");
    }

    private static IResult Forbid(string error, string description)
    {
        var properties = new AuthenticationProperties(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        });
        return Results.Forbid(properties, [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }
}
