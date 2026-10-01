namespace Burdock.Contracts.V1.Pronunciation;

/// <summary>One voice of the catalogue (ADR 0019 §2a): the learner picks one and it is used everywhere.</summary>
public sealed record VoiceDto(string Id, string Name, string Accent, string Gender);

/// <summary>Request to render the pronunciation of one lexeme in the given voices, or in every voice when the list is empty (ADR 0020 §2).</summary>
public sealed record RequestClipsDto(Guid LexemeId, string? Text, IReadOnlyList<string>? Voices);

/// <summary>State of one clip: <c>queued</c>, <c>rendered</c> (then <see cref="Url"/> when a CDN is configured) or <c>failed</c>.</summary>
public sealed record ClipStateDto(string Voice, string Status, Uri? Url, string? Sha256, long Bytes);

/// <summary>All voices of one lexeme after a request or a lookup.</summary>
public sealed record ClipsDto(Guid LexemeId, string Text, string EngineVersion, IReadOnlyList<ClipStateDto> Clips);
