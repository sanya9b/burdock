namespace Burdock.Contracts.V1.Identity;

/// <summary>
/// First call of a fresh installation (spec 09 §3.3.1, FR-09-1): the client generates the installation id (UUIDv4) and a
/// 32-byte secret, keeps both in SecureStorage and registers the anonymous account. Repeating the call with the same
/// pair is idempotent; a different secret for a known id is a conflict.
/// </summary>
public sealed record RegisterInstallationDto(Guid InstallationId, string InstallationSecret, string Platform, string AppVersion, string? Locale);

/// <summary>The anonymous account as the client sees it (no personal data by design, decision T10).</summary>
public sealed record AccountDto(Guid UserId, string Nickname, string Status, DateTimeOffset CreatedAt);

public sealed record RenameAccountDto(string Nickname);
