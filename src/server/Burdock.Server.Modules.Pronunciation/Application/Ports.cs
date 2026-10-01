using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Pronunciation.Domain;

namespace Burdock.Server.Modules.Pronunciation.Application;

/// <summary>Encoded audio returned by the engine: AAC-LC in an M4A container, the same format the pipeline ships (ADR 0019 §1).</summary>
public sealed record RenderedAudio(ReadOnlyMemory<byte> Content, string ContentType);

/// <summary>The synthesis engine behind the module: the Kokoro sidecar in production, a fake in tests.</summary>
public interface IPronunciationRenderer
{
    Task<RenderedAudio> RenderAsync(PronunciationText text, PronunciationVoice voice, CancellationToken cancellationToken);
}

/// <summary>Result of storing a clip: the path it was stored under plus the integrity data recorded on the clip.</summary>
public sealed record StoredClip(string Path, string Sha256, long Bytes);

/// <summary>Where clips live (ADR 0020 §5): a local folder in development, Object Storage behind the CDN in production.</summary>
public interface IClipStore
{
    Task<StoredClip> PutAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken);

    /// <summary>Public (CDN) address of a stored clip, or null when the API has to stream the file itself.</summary>
    Uri? PublicUrl(string path);
}

public interface IClipRepository
{
    Task<Clip?> GetAsync(ClipKey key, CancellationToken cancellationToken);

    Task<IReadOnlyList<Clip>> ListAsync(LexemeId lexemeId, CancellationToken cancellationToken);

    Task SaveAsync(Clip clip, CancellationToken cancellationToken);
}

/// <summary>Hand-off between the request path and the render worker; keys may be enqueued more than once, the worker re-reads the clip state.</summary>
public interface IRenderJobs
{
    ValueTask EnqueueAsync(ClipKey key, CancellationToken cancellationToken);

    IAsyncEnumerable<ClipKey> DequeueAllAsync(CancellationToken cancellationToken);
}
