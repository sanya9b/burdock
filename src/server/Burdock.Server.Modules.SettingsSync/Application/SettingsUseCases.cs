using Burdock.Domain.SharedKernel;
using Burdock.Domain.Sync;
using Burdock.Server.Shared.Time;

namespace Burdock.Server.Modules.SettingsSync.Application;

/// <summary>The learner's settings document as stored.</summary>
public sealed record SettingsDocument(LearnerId LearnerId, int SchemaVersion, IReadOnlyDictionary<string, SettingsEntry> Entries, string Etag, DateTimeOffset UpdatedAt)
{
    public static SettingsDocument Empty(LearnerId learnerId, DateTimeOffset now)
        => new(learnerId, SettingsDocumentRules.CurrentSchemaVersion, new Dictionary<string, SettingsEntry>(StringComparer.Ordinal), SettingsDocumentRules.ComputeEtag([]), now);
}

public interface ISettingsStore
{
    Task<SettingsDocument?> GetAsync(LearnerId learnerId, CancellationToken cancellationToken);

    /// <summary>Writes the document header and only the entries that changed in this merge.</summary>
    Task SaveAsync(SettingsDocument document, IReadOnlyCollection<SettingsEntry> changed, CancellationToken cancellationToken);
}

/// <summary>GET /v1/settings: the stored document, or an empty one for a learner who never synced.</summary>
public sealed class GetSettingsUseCase(ISettingsStore store, IClock clock)
{
    public async Task<SettingsDocument> ExecuteAsync(LearnerId learnerId, CancellationToken cancellationToken)
        => await store.GetAsync(learnerId, cancellationToken).ConfigureAwait(false) ?? SettingsDocument.Empty(learnerId, clock.UtcNow);
}

public sealed record IncomingSetting(string? Key, string? ValueJson, DateTimeOffset UpdatedAt, string? ClientId);

/// <summary>PUT /v1/settings (FR-09-13): validates every entry, merges per key with LWW and returns the resulting document.</summary>
public sealed class PutSettingsUseCase(ISettingsStore store, IClock clock)
{
    public async Task<Result<SettingsDocument>> ExecuteAsync(LearnerId learnerId, int schemaVersion, IReadOnlyList<IncomingSetting> incoming, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (schemaVersion is < 1 or > SettingsDocumentRules.CurrentSchemaVersion)
        {
            return Result.Failure<SettingsDocument>(DomainError.Validation("settings.schema_unsupported", "Errors_Settings_SchemaUnsupported"));
        }

        var entries = new List<SettingsEntry>(incoming.Count);
        foreach (var item in incoming)
        {
            var entry = SettingsEntry.Create(item.Key, item.ValueJson, item.UpdatedAt, item.ClientId);
            if (entry.IsFailure)
            {
                return Result.Failure<SettingsDocument>(entry.Error);
            }

            entries.Add(entry.Value);
        }

        var now = clock.UtcNow;
        var current = await store.GetAsync(learnerId, cancellationToken).ConfigureAwait(false) ?? SettingsDocument.Empty(learnerId, now);
        var (merged, changed) = SettingsDocumentRules.Merge(current.Entries, entries);
        if (merged.Count > SettingsDocumentRules.MaxEntries)
        {
            return Result.Failure<SettingsDocument>(DomainError.Validation("settings.too_many_entries", "Errors_Settings_TooManyEntries", new Dictionary<string, string>(StringComparer.Ordinal) { ["max"] = SettingsDocumentRules.MaxEntries.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        }

        if (changed.Count == 0)
        {
            return Result.Success(current);
        }

        var document = new SettingsDocument(learnerId, SettingsDocumentRules.CurrentSchemaVersion, merged, SettingsDocumentRules.ComputeEtag(merged.Values), now);
        await store.SaveAsync(document, changed, cancellationToken).ConfigureAwait(false);
        return Result.Success(document);
    }
}
