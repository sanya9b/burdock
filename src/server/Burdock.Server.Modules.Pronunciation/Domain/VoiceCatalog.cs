using System.Diagnostics.CodeAnalysis;
using Burdock.Domain.Vocabulary;

namespace Burdock.Server.Modules.Pronunciation.Domain;

/// <summary>
/// The v1 voice selection (ADR 0019 §2a, owner 30.09.2026): the same four Kokoro voices the pipeline renders and the app
/// lists in «Голос». Kept in the domain so the server never renders a voice the client cannot play.
/// </summary>
public static class VoiceCatalog
{
    public static IReadOnlyList<PronunciationVoice> V1 { get; } =
    [
        new(VoiceId.From("af_heart"), "Heart", PronunciationAccent.American, VoiceGender.Female),
        new(VoiceId.From("am_michael"), "Michael", PronunciationAccent.American, VoiceGender.Male),
        new(VoiceId.From("bf_emma"), "Emma", PronunciationAccent.British, VoiceGender.Female),
        new(VoiceId.From("bm_george"), "George", PronunciationAccent.British, VoiceGender.Male),
    ];

    public static bool TryGet(string? id, [NotNullWhen(true)] out PronunciationVoice? voice)
    {
        voice = V1.FirstOrDefault(v => string.Equals(v.Id.Value, id, StringComparison.Ordinal));
        return voice is not null;
    }

    /// <summary>Kokoro language tag of a voice: GA voices read en-us, RP voices en-gb.</summary>
    public static string Language(PronunciationVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        return voice.Accent == PronunciationAccent.British ? "en-gb" : "en-us";
    }
}
