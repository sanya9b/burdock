using System.Globalization;
using Burdock.Application.Ports;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Microsoft.Maui.Storage;

namespace Burdock.Infrastructure.Learner;

/// <summary>
/// Local anonymous learner until the server issues an account (ADR 0006). The id is a random GUID kept in Preferences;
/// settings are stored as scalar keys so they survive schema changes. Replaced by the sync-backed context in phase 2.
/// </summary>
public sealed class PreferencesLearnerContext(IPreferences preferences, IIdGenerator ids) : ILearnerContext
{
    private const string LearnerIdKey = "learner.id";
    private const string LevelKey = "settings.level";
    private const string TopicsKey = "settings.topics";
    private const string NewWordsKey = "settings.newWordsPerDay";
    private const string ExposuresKey = "settings.exposuresPerWordPerDay";
    private const string CadenceKey = "settings.cadenceMinutes";
    private const string NotificationsKey = "settings.notificationsPerDay";
    private const string QuietStartKey = "settings.quietStart";
    private const string QuietEndKey = "settings.quietEnd";
    private const string HideTranslationKey = "settings.translationHidden";
    private const string RetentionKey = "settings.retention";
    private const string GamificationKey = "settings.gamification";
    private const string NicknameKey = "profile.nickname";

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
        preferences.Set(LevelKey, settings.Level.ToString());
        preferences.Set(TopicsKey, string.Join(",", settings.Topics.Select(t => t.Slug)));
        preferences.Set(NewWordsKey, settings.NewWordsPerDay);
        preferences.Set(ExposuresKey, settings.ExposuresPerWordPerDay);
        preferences.Set(CadenceKey, settings.Cadence.Minutes);
        preferences.Set(NotificationsKey, settings.NotificationsPerDay);
        preferences.Set(QuietStartKey, settings.QuietHours.Start.ToString("HH:mm", CultureInfo.InvariantCulture));
        preferences.Set(QuietEndKey, settings.QuietHours.End.ToString("HH:mm", CultureInfo.InvariantCulture));
        preferences.Set(HideTranslationKey, settings.TranslationHiddenOnSurfaces);
        preferences.Set(RetentionKey, settings.Retention.ToString());
        preferences.Set(GamificationKey, settings.GamificationEnabled);
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
        preferences.Set(NicknameKey, profile.Nickname);
        return Task.CompletedTask;
    }

    private static TimeOnly ParseTime(string value)
        => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : new TimeOnly(22, 0);
}
