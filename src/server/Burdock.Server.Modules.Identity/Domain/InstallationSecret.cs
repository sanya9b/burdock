using System.Security.Cryptography;
using Burdock.Domain.SharedKernel;

namespace Burdock.Server.Modules.Identity.Domain;

/// <summary>
/// The installation credential (spec 09 §3.3.1): 32 random bytes the client keeps in SecureStorage and sends over TLS;
/// the server stores only the SHA-256. 256 bits of entropy make the hash unbrute-forceable, so no salt or KDF is needed.
/// </summary>
public readonly record struct InstallationSecret
{
    public const int MinBytes = 32;
    public const int MaxBytes = 64;

    private readonly byte[] _bytes;

    private InstallationSecret(byte[] bytes) => _bytes = bytes;

    public static Result<InstallationSecret> Parse(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return Result.Failure<InstallationSecret>(DomainError.Validation("identity.secret_missing", "Errors_Identity_SecretMissing"));
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(FromUrlSafe(encoded.Trim()));
        }
        catch (FormatException)
        {
            return Result.Failure<InstallationSecret>(DomainError.Validation("identity.secret_invalid", "Errors_Identity_SecretInvalid"));
        }

        if (bytes.Length is < MinBytes or > MaxBytes)
        {
            return Result.Failure<InstallationSecret>(DomainError.Validation("identity.secret_invalid", "Errors_Identity_SecretInvalid"));
        }

        return Result.Success(new InstallationSecret(bytes));
    }

    public byte[] Hash() => SHA256.HashData(_bytes);

    /// <summary>Constant-time comparison against a stored hash.</summary>
    public bool Matches(ReadOnlySpan<byte> storedHash) => CryptographicOperations.FixedTimeEquals(Hash(), storedHash);

    private static string FromUrlSafe(string value)
    {
        var standard = value.Replace('-', '+').Replace('_', '/');
        return (standard.Length % 4) switch
        {
            2 => standard + "==",
            3 => standard + "=",
            _ => standard,
        };
    }
}
