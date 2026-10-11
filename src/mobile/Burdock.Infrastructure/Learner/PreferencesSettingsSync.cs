using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Burdock.Application.Ports;
using Burdock.Domain.Sync;
using Microsoft.Maui.Storage;

namespace Burdock.Infrastructure.Learner;

/// <summary>
/// The synced view of the learner's preferences (spec 09 section 3.6): every key the settings and profile screens write,
/// with the stamp <see cref="PreferencesLearnerContext"/> recorded. Keys without a stamp are defaults and stay local.
/// Values travel as JSON scalars; the server treats them as opaque.
/// </summary>
public sealed class PreferencesSettingsSync(IPreferences preferences) : ISyncableSettings
{
    private static readonly (string Key, ValueKind Kind)[] SyncedKeys =
    [
        (PreferencesLearnerContext.LevelKey, ValueKind.Text),
        (PreferencesLearnerContext.TopicsKey, ValueKind.Text),
        (PreferencesLearnerContext.NewWordsKey, ValueKind.Integer),
        (PreferencesLearnerContext.ExposuresKey, ValueKind.Integer),
        (PreferencesLearnerContext.CadenceKey, ValueKind.Integer),
        (PreferencesLearnerContext.NotificationsKey, ValueKind.Integer),
        (PreferencesLearnerContext.QuietStartKey, ValueKind.Text),
        (PreferencesLearnerContext.QuietEndKey, ValueKind.Text),
        (PreferencesLearnerContext.HideTranslationKey, ValueKind.Flag),
        (PreferencesLearnerContext.RetentionKey, ValueKind.Text),
        (PreferencesLearnerContext.GamificationKey, ValueKind.Flag),
        (PreferencesLearnerContext.NicknameKey, ValueKind.Text),
    ];

    private enum ValueKind
    {
        Text,
        Integer,
        Flag,
    }

    public Task<IReadOnlyList<SettingsEntry>> GetEntriesAsync(string clientId, CancellationToken cancellationToken)
    {
        var entries = new List<SettingsEntry>(SyncedKeys.Length);
        foreach (var (key, kind) in SyncedKeys)
        {
            if (!preferences.ContainsKey(key) || !preferences.ContainsKey(PreferencesLearnerContext.StampPrefix + key))
            {
                continue;
            }

            var ticks = preferences.Get(PreferencesLearnerContext.StampPrefix + key, 0L);
            var client = preferences.Get(PreferencesLearnerContext.ClientPrefix + key, string.Empty);
            var json = kind switch
            {
                ValueKind.Integer => preferences.Get(key, 0).ToString(CultureInfo.InvariantCulture),
                ValueKind.Flag => preferences.Get(key, false) ? "true" : "false",
                _ => JsonSerializer.Serialize(preferences.Get(key, string.Empty), SettingsValueJsonContext.Default.String),
            };
            entries.Add(new SettingsEntry(key, json, new DateTimeOffset(ticks, TimeSpan.Zero), string.IsNullOrEmpty(client) ? clientId : client));
        }

        return Task.FromResult<IReadOnlyList<SettingsEntry>>(entries);
    }

    public Task ApplyAsync(IReadOnlyList<SettingsEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var entry in entries)
        {
            var known = Array.Find(SyncedKeys, k => string.Equals(k.Key, entry.Key, StringComparison.Ordinal));
            if (known.Key is null || !TryWrite(known.Key, known.Kind, entry.ValueJson))
            {
                continue;
            }

            preferences.Set(PreferencesLearnerContext.StampPrefix + entry.Key, entry.UpdatedAt.UtcTicks);
            preferences.Set(PreferencesLearnerContext.ClientPrefix + entry.Key, entry.ClientId);
        }

        return Task.CompletedTask;
    }

    private bool TryWrite(string key, ValueKind kind, string valueJson)
    {
        try
        {
            using var document = JsonDocument.Parse(valueJson);
            var root = document.RootElement;
            switch (kind)
            {
                case ValueKind.Text when root.ValueKind == JsonValueKind.String:
                    preferences.Set(key, root.GetString() ?? string.Empty);
                    return true;
                case ValueKind.Integer when root.ValueKind == JsonValueKind.Number && root.TryGetInt32(out var number):
                    preferences.Set(key, number);
                    return true;
                case ValueKind.Flag when root.ValueKind is JsonValueKind.True or JsonValueKind.False:
                    preferences.Set(key, root.GetBoolean());
                    return true;
                default:
                    return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

[JsonSerializable(typeof(string))]
internal sealed partial class SettingsValueJsonContext : JsonSerializerContext;
