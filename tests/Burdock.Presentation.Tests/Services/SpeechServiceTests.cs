using Burdock.Application.Ports;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Services;
using Microsoft.Maui.Storage;

namespace Burdock.Presentation.Tests.Services;

/// <summary>Voice choice and accent rules of <see cref="SpeechService"/> on the neutral head (playback itself is a platform concern).</summary>
public sealed class SpeechServiceTests
{
    private static readonly PronunciationVoice Heart = new(VoiceId.From("af_heart"), "Heart", PronunciationAccent.American, VoiceGender.Female);
    private static readonly PronunciationVoice George = new(VoiceId.From("bm_george"), "George", PronunciationAccent.British, VoiceGender.Male);

    [Fact]
    public async Task Without_a_choice_the_accent_is_the_default_voice_even_if_an_old_accent_preference_lingers()
    {
        var preferences = new InMemoryPreferences();
        preferences.Set("speech.accent", nameof(PronunciationAccent.British));
        var service = new SpeechService(preferences, new StubAudio([Heart, George]));

        service.Voice.ShouldBeNull();
        service.Accent.ShouldBe(PronunciationAccent.American);
        (await service.GetVoicesAsync(CancellationToken.None)).Select(v => v.Name).ShouldBe(["Heart", "George"]);
    }

    [Fact]
    public void Selecting_a_voice_persists_it_with_its_accent()
    {
        var preferences = new InMemoryPreferences();
        var service = new SpeechService(preferences, new StubAudio([Heart, George]));

        service.SelectVoice(George);

        service.Voice.ShouldBe(George.Id);
        service.Accent.ShouldBe(PronunciationAccent.British);
        preferences.Get("speech.voice", string.Empty).ShouldBe("bm_george");

        new SpeechService(preferences, new StubAudio([Heart, George])).Voice.ShouldBe(George.Id);
    }

    [Fact]
    public void A_corrupt_stored_voice_falls_back_to_no_choice()
    {
        var preferences = new InMemoryPreferences();
        preferences.Set("speech.voice", "not a voice");
        var service = new SpeechService(preferences, new StubAudio([Heart]));

        service.Voice.ShouldBeNull();
        service.Accent.ShouldBe(PronunciationAccent.American);
    }

    private sealed class StubAudio(IReadOnlyList<PronunciationVoice> voices) : IPronunciationAudio
    {
        public Task<IReadOnlyList<PronunciationVoice>> GetVoicesAsync(CancellationToken cancellationToken) => Task.FromResult(voices);

        public Task<PronunciationClip?> FindWordAsync(LexemeId lexemeId, VoiceId voice, CancellationToken cancellationToken) => Task.FromResult<PronunciationClip?>(null);

        public Task<PronunciationClip?> FindSampleAsync(VoiceId voice, CancellationToken cancellationToken) => Task.FromResult<PronunciationClip?>(null);
    }

    /// <summary>Just enough of MAUI Preferences for the service under test.</summary>
    private sealed class InMemoryPreferences : IPreferences
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public bool ContainsKey(string key, string? sharedName = null) => _values.ContainsKey(key);

        public void Remove(string key, string? sharedName = null) => _values.Remove(key);

        public void Clear(string? sharedName = null) => _values.Clear();

        public void Set<T>(string key, T value, string? sharedName = null) => _values[key] = value;

        public T Get<T>(string key, T defaultValue, string? sharedName = null) => _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
    }
}
