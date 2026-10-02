using System.Security.Cryptography;
using Burdock.Contracts.V1;
using Burdock.Server.Modules.Identity.Application;
using Burdock.Server.Modules.Identity.Endpoints;
using Burdock.Server.Modules.Identity.Infrastructure;
using Burdock.Server.Shared.Modules;
using Burdock.Server.Shared.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Burdock.Server.Modules.Identity;

/// <summary>Anonymous accounts, installation credentials and token issuance through OpenIddict (spec 09 §3.3, decision T13).</summary>
public sealed class IdentityModule : IModule
{
    public string Name => "identity";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        services.AddSingleton(auth);
        services.AddModuleDbContext<IdentityDbContext>(configuration, IdentityDbContext.Schema, options => options.UseOpenIddict());

        services.AddScoped<EfIdentityStores>();
        services.AddScoped<IAccountStore>(static sp => sp.GetRequiredService<EfIdentityStores>());
        services.AddScoped<IInstallationStore>(static sp => sp.GetRequiredService<EfIdentityStores>());
        services.AddScoped<IIdentityUnitOfWork>(static sp => sp.GetRequiredService<EfIdentityStores>());
        services.AddSingleton<IRandomSource, CryptoRandomSource>();
        services.AddScoped<RegisterInstallationUseCase>();
        services.AddScoped<AuthenticateInstallationUseCase>();
        services.AddScoped<GetAccountUseCase>();
        services.AddScoped<RenameAccountUseCase>();

        services.AddOpenIddict()
            .AddCore(core => core.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>())
            .AddServer(server =>
            {
                server.SetTokenEndpointUris(ApiRoutes.Connect.Token)
                    .SetRevocationEndpointUris(ApiRoutes.Connect.Revocation);
                server.AllowRefreshTokenFlow()
                    .AllowCustomFlow(ApiRoutes.Connect.InstallationGrantType);
                server.RegisterScopes(Scopes.OfflineAccess);
                server.SetAccessTokenLifetime(auth.AccessTokenLifetime)
                    .SetRefreshTokenLifetime(auth.RefreshTokenLifetime)
                    .SetRefreshTokenReuseLeeway(auth.RefreshTokenReuseLeeway);
                server.UseReferenceRefreshTokens();
                server.DisableAccessTokenEncryption();
                if (auth.Issuer is not null)
                {
                    server.SetIssuer(auth.Issuer);
                }

                if (!string.IsNullOrWhiteSpace(auth.SigningKeyPem))
                {
                    var ecdsa = ECDsa.Create();
                    ecdsa.ImportFromPem(auth.SigningKeyPem);
                    server.AddSigningKey(new ECDsaSecurityKey(ecdsa));
                }
                else
                {
                    server.AddEphemeralSigningKey();
                }

                if (!string.IsNullOrWhiteSpace(auth.EncryptionKeyBase64))
                {
                    server.AddEncryptionKey(new SymmetricSecurityKey(Convert.FromBase64String(auth.EncryptionKeyBase64)));
                }
                else
                {
                    server.AddEphemeralEncryptionKey();
                }

                var aspnet = server.UseAspNetCore().EnableTokenEndpointPassthrough();
                if (auth.AllowInsecureHttp)
                {
                    aspnet.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(validation =>
            {
                validation.UseLocalServer();
                validation.UseAspNetCore();
            });

        services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        services.AddHostedService<MobileClientSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        IdentityEndpoints.Map(endpoints);
        ConnectEndpoints.Map(endpoints);
    }
}
