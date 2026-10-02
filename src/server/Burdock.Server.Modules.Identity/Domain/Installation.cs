using Burdock.Domain.SharedKernel;

namespace Burdock.Server.Modules.Identity.Domain;

public enum ClientPlatform
{
    Ios,
    Android,
}

/// <summary>One app installation bound to an account (spec 09 §3.3.1): id and secret hash are the anonymous credential.</summary>
public sealed class Installation
{
    public const int MaxAppVersionLength = 32;

    private Installation(Guid id, LearnerId userId, byte[] secretHash, ClientPlatform platform, string appVersion, DateTimeOffset createdAt, DateTimeOffset lastSeenAt, DateTimeOffset? revokedAt)
    {
        Id = id;
        UserId = userId;
        SecretHash = secretHash;
        Platform = platform;
        AppVersion = appVersion;
        CreatedAt = createdAt;
        LastSeenAt = lastSeenAt;
        RevokedAt = revokedAt;
    }

    public Guid Id { get; }

    public LearnerId UserId { get; }

    public byte[] SecretHash { get; }

    public ClientPlatform Platform { get; }

    public string AppVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    public static Result<Installation> Register(Guid id, LearnerId userId, InstallationSecret secret, ClientPlatform platform, string? appVersion, DateTimeOffset now)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Installation>(DomainError.Validation("identity.installation_invalid", "Errors_Identity_InstallationInvalid"));
        }

        var version = NormaliseVersion(appVersion);
        if (version.IsFailure)
        {
            return Result.Failure<Installation>(version.Error);
        }

        return Result.Success(new Installation(id, userId, secret.Hash(), platform, version.Value, now, now, null));
    }

    public static Installation Restore(Guid id, LearnerId userId, byte[] secretHash, ClientPlatform platform, string appVersion, DateTimeOffset createdAt, DateTimeOffset lastSeenAt, DateTimeOffset? revokedAt)
        => new(id, userId, secretHash, platform, appVersion, createdAt, lastSeenAt, revokedAt);

    public static Result<ClientPlatform> ParsePlatform(string? platform) => platform?.Trim().ToUpperInvariant() switch
    {
        "IOS" => Result.Success(ClientPlatform.Ios),
        "ANDROID" => Result.Success(ClientPlatform.Android),
        _ => Result.Failure<ClientPlatform>(DomainError.Validation("identity.platform_invalid", "Errors_Identity_PlatformInvalid")),
    };

    public bool Matches(InstallationSecret secret) => secret.Matches(SecretHash);

    public void Seen(string? appVersion, DateTimeOffset now)
    {
        var version = NormaliseVersion(appVersion);
        if (version.IsSuccess)
        {
            AppVersion = version.Value;
        }

        if (now > LastSeenAt)
        {
            LastSeenAt = now;
        }
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    private static Result<string> NormaliseVersion(string? appVersion)
    {
        var trimmed = (appVersion ?? string.Empty).Trim();
        return trimmed.Length is 0 or > MaxAppVersionLength || trimmed.Any(char.IsControl)
            ? Result.Failure<string>(DomainError.Validation("identity.app_version_invalid", "Errors_Identity_AppVersionInvalid"))
            : Result.Success(trimmed);
    }
}
