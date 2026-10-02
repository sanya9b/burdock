namespace Burdock.Server.Modules.Identity.Infrastructure;

/// <summary>«Auth» section (spec 09 §3.15). Keys come from sops in production; without them the server falls back to ephemeral keys, which is fine only for Development and tests.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan RefreshTokenReuseLeeway { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>PEM-encoded ECDSA P-256 private key used to sign access tokens (ES256).</summary>
    public string? SigningKeyPem { get; set; }

    /// <summary>Base64 256-bit symmetric key OpenIddict uses for the tokens it encrypts.</summary>
    public string? EncryptionKeyBase64 { get; set; }

    /// <summary>Issuer written into tokens (https://api.burdock.org/); unset derives it from the request.</summary>
    public Uri? Issuer { get; set; }

    /// <summary>Lets the token endpoint answer over plain HTTP: Development and the in-process test server only; production terminates TLS at Caddy and forwards the scheme.</summary>
    public bool AllowInsecureHttp { get; set; }
}
