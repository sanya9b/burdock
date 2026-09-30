namespace Burdock.Domain.Vocabulary;

/// <summary>Pronunciation variety of a voice and of the transcription shown next to a word (spec 12 §3.9): GA or RP.</summary>
public enum PronunciationAccent
{
    American,
    British,
}

public enum VoiceGender
{
    Female,
    Male,
}

/// <summary>Identifier of a rendered voice as the content pipeline names it (for example «af_heart»).</summary>
public readonly record struct VoiceId
{
    public VoiceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 32 || !value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_'))
        {
            throw new ArgumentException("VoiceId is 1-32 lowercase ASCII letters, digits or underscores.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static VoiceId From(string value) => new(value);

    public override string ToString() => Value;
}

/// <summary>One voice of the content catalogue (ADR 0019): the learner picks one and it is used everywhere.</summary>
public sealed record PronunciationVoice(VoiceId Id, string Name, PronunciationAccent Accent, VoiceGender Gender);
