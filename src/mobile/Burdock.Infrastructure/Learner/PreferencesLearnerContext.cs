using System.Globalization;
using Burdock.Application.Ports;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Microsoft.Maui.Storage;

namespace Burdock.Infrastructure.Learner;

/// <summary>
/// Local anonymous learner (ADR 0006). The id is a random GUID kept in Preferences and proposed to the server as the
/// account id at registration; settings are stored as scalar keys so they survive schema changes. Every write of a
/// changed value stamps the key with the clock, which is what the settings sync sends as the last-writer-wins stamp
/// (spec 09 section 3.6); <see cref="PreferencesSettingsSync"/> reads and applies those stamps.
/// </summary>
public sealed class PreferencesLearnerContext(IPreferences preferences, IIdGenerator ids, IClock clock) : ILearnerContext
{
    internal const string LearnerIdKey = "learner.id";
    internal const string LevelKey = "settings.level";
    internal const string TopicsKey = "settings.topics";
    internal const string NewWordsKey = "settings.newWordsPerDay";
    internal const string ExposuresKey = "settings.exposuresPerWordPerDay";
    internal const string CadenceKey = "settings.cadenceMinutes";
    internal const string NotificationsKey = "settings.notificationsPerDay";
    internal const string QuietStartKey = "settings.quietStart";
    internal const string QuietEndKey = "settings.quietEnd";
    internal const string HideTranslationKey = "settings.translationHidden";
    internal const string RetentionKey = "settings.retention";
    internal const string GamificationKey = "settings.gamification";
    internal const string NicknameKey = "profile.nickname";

    /// <summary>UTC ticks of the last local or applied change of a synced key.</summary>
    internal const string StampPrefix = "sync.stamp.";

    /// <summary>Client id of the device that wrote the current value; absent for values written on this device.</summary>
    internal const string ClientPrefix = "sync.client.";

    /// <summary>The nine launch categories (decision Q18 with the 28.09.2026 addendum) in display order.</summary>
    public static readonly IReadOnlyList<TopicId> DefaultTopics =
        new[] { "communication", "travel", "work", "it", "food", "health", "city", "shopping", "military" }.Select(TopicId.From).ToArray();

    public Task<LearnerId> GetLearnerIdAsync(CancellationToken cancellationToken)
    {
        var stored = preferences.Get(LearnerIdKey, string.Empty);
        if (Guid.TryParse(stored, out var existing) && existing != Guid.Empty)
        {
            return Task.FromResult(LearnerId.From(existing));
        }

        var created = ids.NewId();
        preferences.Set(LearnerIdKey, created.ToString("D"));
        return Task.FromResult(LearnerId.From(created));
    }

    public Task<LearnerSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var defaults = LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, DefaultTopics);
        if (!preferences.ContainsKey(LevelKey))
        {
            return Task.FromResult(defaults);
        }

        var topics = preferences.Get(TopicsKey, string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(TopicId.From).ToArray();
        var settings = defaults with
        {
            Level = Enum.TryParse<CefrLevel>(preferences.Get(LevelKey, "A1"), out var level) ? level : CefrLevel.A1,
            Topics = topics.Length > 0 ? topics : DefaultTopics,
            NewWordsPerDay = preferences.Get(NewWordsKey, defaults.NewWordsPerDay),
            ExposuresPerWordPerDay = preferences.Get(ExposuresKey, defaults.ExposuresPerWordPerDay),
            Cadence = RotationCadence.Create(preferences.Get(CadenceKey, defaults.Cadence.Minutes)).Match(c => c, _ => defaults.Cadence),
            NotificationsPerDay = preferences.Get(NotificationsKey, defaults.NotificationsPerDay),
            QuietHours = new QuietHours(ParseTime(preferences.Get(QuietStartKey, "22:00")), ParseTime(preferences.Get(QuietEndKey, "08:00"))),
            TranslationHiddenOnSurfaces = preferences.Get(HideTranslationKey, false),
            Retention = Enum.TryParse<RetentionPreset>(preferences.Get(RetentionKey, "Standard"), out var retention) ? retention : RetentionPreset.Standard,
            GamificationEnabled = preferences.Get(GamificationKey, true),
        };
        return Task.FromResult(settings);
    }

    public Task SaveSettingsAsync(LearnerSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var now = clock.UtcNow;
        Write(LevelKey, settings.Level.ToString(), now);
        Write(TopicsKey, string.Join(",", settings.Topics.Select(t => t.Slug)), now);
        Write(NewWordsKey, settings.NewWordsPerDay, now);
        Write(ExposuresKey, settings.ExposuresPerWordPerDay, now);
        Write(CadenceKey, settings.Cadence.Minutes, now);
        Write(NotificationsKey, settings.NotificationsPerDay, now);
        Write(QuietStartKey, settings.QuietHours.Start.ToString("HH:mm", CultureInfo.InvariantCulture), now);
        Write(QuietEndKey, settings.QuietHours.End.ToString("HH:mm", CultureInfo.InvariantCulture), now);
        Write(HideTranslationKey, settings.TranslationHiddenOnSurfaces, now);
        Write(RetentionKey, settings.Retention.ToString(), now);
        Write(GamificationKey, settings.GamificationEnabled, now);
        return Task.CompletedTask;
    }

    public Task<LearnerProfile?> GetProfileAsync(CancellationToken cancellationToken)
    {
        var nickname = preferences.Get(NicknameKey, string.Empty);
        return Task.FromResult(LearnerProfile.Create(nickname).Match(p => (LearnerProfile?)p, _ => null));
    }

    public Task SaveProfileAsync(LearnerProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Write(NicknameKey, profile.Nickname, clock.UtcNow);
        return Task.CompletedTask;
    }

    /// <summary>Writes a value only when it changed, and stamps it as a local change.</summary>
    private void Write<T>(string key, T value, DateTimeOffset now)
    {
        if (preferences.ContainsKey(key) && EqualityComparer<T>.Default.Equals(preferences.Get(key, value), value))
        {
            return;
        }

        preferences.Set(key, value);
        preferences.Set(StampPrefix + key, Burdock.Domain.Sync.SettingsEntry.NormalizeStamp(now).UtcTicks);
        preferences.Remove(ClientPrefix + key);
    }

    private static TimeOnly ParseTime(string value)
        => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : new TimeOnly(22, 0);
}
