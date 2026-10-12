using System.Globalization;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Server.Modules.Pronunciation.Domain;

public enum ClipStatus
{
    Queued,
    Rendered,
    Failed,
}

/// <summary>Who asked for the clip (ADR 0020 §1); the learner's own words arrive with a later ADR.</summary>
public enum ClipRequester
{
    Pipeline,
    Admin,
    User,
}

/// <summary>One clip is one lexeme in one voice; the engine version is part of the storage path, not of the key.</summary>
public readonly record struct ClipKey(LexemeId LexemeId, VoiceId Voice);

/// <summary>Lifecycle of a rendered pronunciation (ADR 0020 §1): queued → rendered, or queued → failed after <see cref="MaxAttempts"/>.</summary>
public sealed record Clip(
    ClipKey Key,
    PronunciationText Text,
    string EngineVersion,
    ClipStatus Status,
    string? Path,
    string? Sha256,
    long Bytes,
    ClipRequester RequestedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RenderedAt,
    int Attempts,
    string? LastError)
{
    public const int MaxAttempts = 3;

    public static Clip Queued(ClipKey key, PronunciationText text, string engineVersion, ClipRequester requestedBy, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);
        return new Clip(key, text, engineVersion, ClipStatus.Queued, null, null, 0, requestedBy, now, null, 0, null);
    }

    public Clip Rendered(string path, string sha256, long bytes, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        return this with { Status = ClipStatus.Rendered, Path = path, Sha256 = sha256, Bytes = bytes, RenderedAt = now, LastError = null };
    }

    /// <summary>Records a failed attempt; the clip stays queued until the attempts run out.</summary>
    public Clip FailedAttempt(string error)
    {
        var attempts = Attempts + 1;
        return this with { Attempts = attempts, LastError = error, Status = attempts >= MaxAttempts ? ClipStatus.Failed : ClipStatus.Queued };
    }

    /// <summary>Immutable, versioned location (ADR 0020 §5): a new engine version renders next to the old clips instead of overwriting them.</summary>
    public static string StoragePath(string engineVersion, ClipKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);
        return string.Create(CultureInfo.InvariantCulture, $"pronunciation/{engineVersion}/{key.Voice.Value}/{key.LexemeId.Value:D}.m4a");
    }

    /// <summary>The voice's sample phrase for the voice picker lives next to its clips.</summary>
    public static string SamplePath(string engineVersion, VoiceId voice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);
        return string.Create(CultureInfo.InvariantCulture, $"pronunciation/{engineVersion}/{voice.Value}/sample.m4a");
    }
}
