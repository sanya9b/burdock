using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Modules.Pronunciation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Burdock.Server.Modules.Pronunciation.Infrastructure;

/// <summary>EF Core repository over the scoped <see cref="PronunciationDbContext"/>; every save is its own unit of work, so the worker and the request path never share a tracked row.</summary>
public sealed class EfClipRepository(PronunciationDbContext db) : IClipRepository
{
    public async Task<Clip?> GetAsync(ClipKey key, CancellationToken cancellationToken)
    {
        var lexemeId = key.LexemeId.Value;
        var voice = key.Voice.Value;
        var row = await db.Clips.AsNoTracking().FirstOrDefaultAsync(c => c.LexemeId == lexemeId && c.Voice == voice, cancellationToken).ConfigureAwait(false);
        return row is null ? null : ToDomain(row);
    }

    public async Task<IReadOnlyList<Clip>> ListAsync(LexemeId lexemeId, CancellationToken cancellationToken)
    {
        var id = lexemeId.Value;
        var rows = await db.Clips.AsNoTracking().Where(c => c.LexemeId == id).OrderBy(c => c.Voice).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<ClipKey>> ListQueuedAsync(string engineVersion, int limit, CancellationToken cancellationToken)
    {
        var queued = Name(ClipStatus.Queued);
        var rows = await db.Clips.AsNoTracking()
            .Where(c => c.Status == queued && c.EngineVersion == engineVersion)
            .OrderBy(c => c.CreatedAt)
            .Take(limit)
            .Select(c => new { c.LexemeId, c.Voice })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(r => new ClipKey(LexemeId.From(r.LexemeId), VoiceId.From(r.Voice))).ToArray();
    }

    public async Task SaveAsync(Clip clip, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clip);
        var row = await db.Clips.FindAsync([clip.Key.LexemeId.Value, clip.Key.Voice.Value], cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = new ClipRow { LexemeId = clip.Key.LexemeId.Value, Voice = clip.Key.Voice.Value };
            db.Clips.Add(row);
        }

        row.EngineVersion = clip.EngineVersion;
        row.Text = clip.Text.Value;
        row.Status = Name(clip.Status);
        row.Path = clip.Path;
        row.Sha256 = clip.Sha256;
        row.Bytes = clip.Bytes;
        row.RequestedBy = clip.RequestedBy.ToString().ToLowerInvariant();
        row.CreatedAt = clip.CreatedAt;
        row.RenderedAt = clip.RenderedAt;
        row.Attempts = clip.Attempts;
        row.LastError = clip.LastError;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        db.ChangeTracker.Clear();
    }

    private static string Name(ClipStatus status) => status.ToString().ToLowerInvariant();

    private static Clip ToDomain(ClipRow row)
    {
        var text = PronunciationText.Create(row.Text);
        if (text.IsFailure)
        {
            throw new InvalidOperationException($"Stored clip text for {row.LexemeId:D}/{row.Voice} is invalid: {text.Error.Code}");
        }

        return new Clip(
            new ClipKey(LexemeId.From(row.LexemeId), VoiceId.From(row.Voice)),
            text.Value,
            row.EngineVersion,
            Enum.Parse<ClipStatus>(row.Status, ignoreCase: true),
            row.Path,
            row.Sha256,
            row.Bytes,
            Enum.Parse<ClipRequester>(row.RequestedBy, ignoreCase: true),
            row.CreatedAt,
            row.RenderedAt,
            row.Attempts,
            row.LastError);
    }
}
