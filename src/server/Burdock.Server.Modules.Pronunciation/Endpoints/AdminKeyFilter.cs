using System.Security.Cryptography;
using System.Text;
using Burdock.Contracts.V1;
using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Shared.Errors;
using Microsoft.AspNetCore.Http;

namespace Burdock.Server.Modules.Pronunciation.Endpoints;

/// <summary>
/// v1 guard of the write endpoints (ADR 0020 §2): a shared admin key in <see cref="ApiRoutes.Pronunciation.AdminKeyHeader"/>,
/// compared in constant time. Replaced by an OpenIddict scope once the Identity module exists. No key configured → 503, so a
/// misconfigured deployment fails closed.
/// </summary>
public sealed class AdminKeyFilter(PronunciationOptions options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (string.IsNullOrEmpty(options.AdminKey))
        {
            return DomainError.Unavailable("pronunciation.admin_disabled", "Errors_Pronunciation_AdminDisabled").ToProblem();
        }

        var presented = context.HttpContext.Request.Headers[ApiRoutes.Pronunciation.AdminKeyHeader].ToString();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(options.AdminKey)))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "pronunciation.admin_key_invalid",
                type: "https://burdock.org/problems/pronunciation.admin_key_invalid");
        }

        return await next(context).ConfigureAwait(false);
    }
}
