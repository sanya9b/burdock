using System.Collections.Concurrent;
using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Modules.Pronunciation.Domain;

namespace Burdock.Server.Modules.Pronunciation.Infrastructure;

/// <summary>v1 repository: state lives in the process and is lost on restart; the PostgreSQL table <c>pronunciation.clip</c> replaces it in phase 2.</summary>
public sealed class InMemoryClipRepository : IClipRepository
{
    private readonly ConcurrentDictionary<ClipKey, Clip> _clips = new();

    public Task<Clip?> GetAsync(ClipKey key, CancellationToken cancellationToken)
        => Task.FromResult(_clips.TryGetValue(key, out var clip) ? clip : null);

    public Task<IReadOnlyList<Clip>> ListAsync(LexemeId lexemeId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Clip>>(_clips.Values.Where(c => c.Key.LexemeId == lexemeId).OrderBy(c => c.Key.Voice.Value, StringComparer.Ordinal).ToArray());

    public Task SaveAsync(Clip clip, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clip);
        _clips[clip.Key] = clip;
        return Task.CompletedTask;
    }
}
