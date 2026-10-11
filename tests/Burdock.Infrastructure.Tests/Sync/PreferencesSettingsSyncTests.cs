using Burdock.Application.Ports;
using Burdock.Domain.Learning;
using Burdock.Domain.Sync;
using Burdock.Domain.Vocabulary;
using Burdock.Infrastructure.Learner;
using Microsoft.Maui.Storage;

namespace Burdock.Infrastructure.Tests.Sync;

/// <summary>Per-key stamps written by the learner context and read or applied by the settings sync (spec 09 section 3.6).</summary>
public sealed class PreferencesSettingsSyncTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryPreferences _preferences = new();
    private readonly MutableClock _clock = new() { UtcNow = T0 };
    private readonly PreferencesLearnerContext _context;
    private readonly PreferencesSettingsSync _sync;

    public PreferencesSettingsSyncTests()
    {
        _context = new PreferencesLearnerContext(_preferences, new RandomIds(), _clock);
        _sync = new PreferencesSettingsSync(_preferences);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static LearnerSettings Moderate => LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, PreferencesLearnerContext.DefaultTopics);

    [Fact]
    public async Task Nothing_is_synced_before_the_learner_saved_anything()
    {
        (await _sync.GetEntriesAsync("device", Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Saving_stamps_every_key_and_a_later_save_stamps_only_what_changed()
    {
        await _context.SaveSettingsAsync(Moderate, Ct);
        var first = await _sync.GetEntriesAsync("device", Ct);

        first.Count.ShouldBe(11);
        first.ShouldAllBe(e => e.UpdatedAt == T0 && e.ClientId == "device");
        first.Single(e => e.Key == "settings.level").ValueJson.ShouldBe("\"A1\"");
        first.Single(e => e.Key == "settings.newWordsPerDay").ValueJson.ShouldBe("10");
        first.Single(e => e.Key == "settings.gamification").ValueJson.ShouldBe("true");
        first.Single(e => e.Key == "settings.topics").ValueJson.ShouldStartWith("\"communication,travel,");

        _clock.UtcNow = T0.AddHours(1);
        await _context.SaveSettingsAsync(Moderate with { Level = CefrLevel.B1 }, Ct);
        var second = await _sync.GetEntriesAsync("device", Ct);

        second.Single(e => e.Key == "settings.level").UpdatedAt.ShouldBe(T0.AddHours(1));
        second.Single(e => e.Key == "settings.level").ValueJson.ShouldBe("\"B1\"");
        second.Single(e => e.Key == "settings.newWordsPerDay").UpdatedAt.ShouldBe(T0);
    }

    [Fact]
    public async Task Applied_entries_change_the_settings_and_keep_their_origin_while_malformed_or_unknown_ones_are_ignored()
    {
        await _context.SaveSettingsAsync(Moderate, Ct);
        var later = T0.AddMinutes(5);

        await _sync.ApplyAsync(
        [
            new SettingsEntry("settings.level", "\"B2\"", later, "other"),
            new SettingsEntry("settings.newWordsPerDay", "15", later, "other"),
            new SettingsEntry("settings.gamification", "false", later, "other"),
            new SettingsEntry("settings.retention", "{not json", later, "other"),
            new SettingsEntry("settings.notificationsPerDay", "\"five\"", later, "other"),
            new SettingsEntry("settings.unknown", "1", later, "other"),
        ], Ct);

        var settings = await _context.GetSettingsAsync(Ct);
        settings.Level.ShouldBe(CefrLevel.B2);
        settings.NewWordsPerDay.ShouldBe(15);
        settings.GamificationEnabled.ShouldBeFalse();
        settings.Retention.ShouldBe(RetentionPreset.Standard);
        settings.NotificationsPerDay.ShouldBe(3);
        var entries = await _sync.GetEntriesAsync("device", Ct);
        entries.Single(e => e.Key == "settings.level").ShouldBe(new SettingsEntry("settings.level", "\"B2\"", later, "other"));
        entries.Single(e => e.Key == "settings.retention").ClientId.ShouldBe("device");
        entries.Single(e => e.Key == "settings.notificationsPerDay").UpdatedAt.ShouldBe(T0);
        entries.ShouldNotContain(e => e.Key == "settings.unknown");
    }

    [Fact]
    public async Task A_local_change_after_an_applied_one_becomes_local_again()
    {
        await _sync.ApplyAsync([new SettingsEntry("settings.level", "\"B2\"", T0, "other")], Ct);
        _clock.UtcNow = T0.AddMinutes(1);

        await _context.SaveSettingsAsync(Moderate with { Level = CefrLevel.C1 }, Ct);

        var level = (await _sync.GetEntriesAsync("device", Ct)).Single(e => e.Key == "settings.level");
        level.ValueJson.ShouldBe("\"C1\"");
        level.ClientId.ShouldBe("device");
        level.UpdatedAt.ShouldBe(T0.AddMinutes(1));
    }

    [Fact]
    public async Task The_nickname_travels_as_a_json_string()
    {
        await _context.SaveProfileAsync(LearnerProfile.Create("Olenka").Value, Ct);

        (await _sync.GetEntriesAsync("device", Ct)).ShouldBe([new SettingsEntry("profile.nickname", "\"Olenka\"", T0, "device")]);
        await _sync.ApplyAsync([new SettingsEntry("profile.nickname", "\"Reppy\"", T0.AddMinutes(1), "other")], Ct);
        (await _context.GetProfileAsync(Ct))!.Nickname.ShouldBe("Reppy");
    }

    private sealed class RandomIds : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private sealed class MutableClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }

        public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}

/// <summary>Just enough of MAUI Preferences for the adapters under test.</summary>
internal sealed class InMemoryPreferences : IPreferences
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public bool ContainsKey(string key, string? sharedName = null) => _values.ContainsKey(key);

    public void Remove(string key, string? sharedName = null) => _values.Remove(key);

    public void Clear(string? sharedName = null) => _values.Clear();

    public void Set<T>(string key, T value, string? sharedName = null) => _values[key] = value;

    public T Get<T>(string key, T defaultValue, string? sharedName = null) => _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
}
