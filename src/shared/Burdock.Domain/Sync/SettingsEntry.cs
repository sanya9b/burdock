using System.Security.Cryptography;
using System.Text;
using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Sync;

/// <summary>
/// One synchronised setting with its last-writer-wins stamp (spec 09 §3.6, FR-09-13). Shared by the app and the server so
/// both sides merge identically. Values are opaque JSON; keys and their meaning belong to specs 05/06 and the app.
/// </summary>
public sealed record SettingsEntry(string Key, string ValueJson, DateTimeOffset UpdatedAt, string ClientId)
{
    public const int MaxKeyLength = 64;
    public const int MaxValueBytes = 4096;
    public const int MaxClientIdLength = 64;

    public static Result<SettingsEntry> Create(string? key, string? valueJson, DateTimeOffset updatedAt, string? clientId)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength || !key.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
        {
            return Result.Failure<SettingsEntry>(DomainError.Validation("settings.key_invalid", "Errors_Settings_KeyInvalid", new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = key ?? string.Empty }));
        }

        if (string.IsNullOrWhiteSpace(valueJson) || Encoding.UTF8.GetByteCount(valueJson) > MaxValueBytes)
        {
            return Result.Failure<SettingsEntry>(DomainError.Validation("settings.value_invalid", "Errors_Settings_ValueInvalid", new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = key }));
        }

        if (string.IsNullOrWhiteSpace(clientId) || clientId.Length > MaxClientIdLength || clientId.Any(char.IsControl))
        {
            return Result.Failure<SettingsEntry>(DomainError.Validation("settings.client_invalid", "Errors_Settings_ClientInvalid", new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = key }));
        }

        return Result.Success(new SettingsEntry(key, valueJson, updatedAt, clientId.Trim()));
    }

    /// <summary>LWW: the later stamp wins; on an equal stamp the lexicographically larger client id wins, which both sides compute identically.</summary>
    public bool Supersedes(SettingsEntry other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return UpdatedAt > other.UpdatedAt || (UpdatedAt == other.UpdatedAt && string.CompareOrdinal(ClientId, other.ClientId) > 0);
    }
}

/// <summary>Pure merge and fingerprint of a settings document (spec 09 §3.6).</summary>
public static class SettingsDocumentRules
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxEntries = 200;

    /// <summary>Applies incoming entries per key; returns the merged map and the entries that actually changed.</summary>
    public static (IReadOnlyDictionary<string, SettingsEntry> Merged, IReadOnlyList<SettingsEntry> Changed) Merge(IReadOnlyDictionary<string, SettingsEntry> current, IEnumerable<SettingsEntry> incoming)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(incoming);
        var merged = new Dictionary<string, SettingsEntry>(current, StringComparer.Ordinal);
        var changed = new List<SettingsEntry>();
        foreach (var entry in incoming)
        {
            if (!merged.TryGetValue(entry.Key, out var existing) || entry.Supersedes(existing))
            {
                merged[entry.Key] = entry;
                changed.Add(entry);
            }
        }

        return (merged, changed);
    }

    /// <summary>Stable fingerprint of the document: keys in ordinal order with their stamps and values.</summary>
    public static string ComputeEtag(IEnumerable<SettingsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var builder = new StringBuilder();
        foreach (var entry in entries.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            builder.Append(entry.Key).Append('=').Append(entry.UpdatedAt.UtcTicks).Append('|').Append(entry.ClientId).Append('|').Append(entry.ValueJson).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))[..16];
    }
}
