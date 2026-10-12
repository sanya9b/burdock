using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Burdock.Server.Api;

/// <summary>
/// Shapes the generated OpenAPI document into the published contract (spec 09 FR-09-25, spec 16 FR-16-11): stable
/// info and server, a bearer security scheme and a requirement on every endpoint that needs a token. The document is
/// snapshotted into <c>docs/api/openapi.v1.json</c> by <c>Burdock.Server.ContractTests</c>.
/// </summary>
public static class OpenApiContract
{
    public const string DocumentName = "v1";
    public const string BearerScheme = "bearer";
    public const string PublicServerUrl = "https://api.burdock.org";

    public static void Configure(OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddDocumentTransformer(static (document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "Burdock API",
                Version = DocumentName,
                Description = "API v1 of the Burdock (Реп’ях) vocabulary app: anonymous identity, content manifest, progress and settings sync, pronunciation. Tokens come from POST /v1/connect/token (OAuth 2.0, installation grant and refresh_token).",
            };
            document.Servers = [new OpenApiServer { Url = PublicServerUrl }];
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
            document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "opaque",
                Description = "Access token issued by POST /v1/connect/token.",
            };
            return Task.CompletedTask;
        });

        options.AddOperationTransformer(static (operation, context, _) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            var requiresToken = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
            if (requiresToken)
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
                });
            }

            return Task.CompletedTask;
        });
    }
}
