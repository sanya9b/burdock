using Burdock.Application.Ports;
using Burdock.Contracts.Content;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Infrastructure.Content;

/// <summary>In-memory view of one audio-index.json: voices in catalogue order and clip lookups by (lexeme, voice).</summary>
public sealed class AudioCatalog
{
    private readonly Dictionary<(VoiceId Voice, LexemeId Lexeme), PronunciationClip> _clips = [];
    private readonly Dictionary<VoiceId, PronunciationClip> _samples = [];

    private AudioCatalog(IReadOnlyList<PronunciationVoice> voices)
    {
        Voices = voices;
    }

    public static AudioCatalog Empty { get; } = new([]);

    public IReadOnlyList<PronunciationVoice> Voices { get; }

    /// <summary>Maps the transport document; <paramref name="assetRoot"/> prefixes every file path (for example «content/audio/»).</summary>
    public static Result<AudioCatalog> FromDocument(AudioIndexDocument document, string assetRoot)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(assetRoot);
        if (document.SchemaVersion > AudioIndexDocument.CurrentSchemaVersion)
        {
            return DomainError.Validation("audio.schemaTooNew", "Errors_Content_SchemaTooNew");
        }

        var voices = new List<PronunciationVoice>(document.Voices.Count);
        var catalog = new AudioCatalog(voices);
        foreach (var voice in document.Voices)
        {
            VoiceId id;
            try
            {
                id = VoiceId.From(voice.Id);
            }
            catch (ArgumentException)
            {
                return DomainError.Validation("audio.voiceId", "Errors_Content_InvalidVoice", new Dictionary<string, string> { ["voice"] = voice.Id });
            }

            var accent = string.Equals(voice.Accent, "RP", StringComparison.OrdinalIgnoreCase) ? PronunciationAccent.British : PronunciationAccent.American;
            var gender = string.Equals(voice.Gender, "male", StringComparison.OrdinalIgnoreCase) ? VoiceGender.Male : VoiceGender.Female;
            voices.Add(new PronunciationVoice(id, voice.Name, accent, gender));
            catalog._samples[id] = new PronunciationClip(assetRoot + voice.Sample.Path);
            foreach (var (lexeme, file) in voice.Files)
            {
                if (Guid.TryParse(lexeme, out var guid) && guid != Guid.Empty)
                {
                    catalog._clips[(id, LexemeId.From(guid))] = new PronunciationClip(assetRoot + file.Path);
                }
            }
        }

        return catalog;
    }

    public PronunciationClip? FindWord(LexemeId lexemeId, VoiceId voice) => _clips.GetValueOrDefault((voice, lexemeId));

    public PronunciationClip? FindSample(VoiceId voice) => _samples.GetValueOrDefault(voice);
}
