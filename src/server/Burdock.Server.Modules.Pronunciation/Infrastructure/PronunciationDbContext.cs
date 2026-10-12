using Microsoft.EntityFrameworkCore;

namespace Burdock.Server.Modules.Pronunciation.Infrastructure;

/// <summary>Schema <c>pronunciation</c> (ADR 0020 §1): one row per lexeme and voice with the lifecycle of its clip.</summary>
public sealed class PronunciationDbContext(DbContextOptions<PronunciationDbContext> options) : DbContext(options)
{
    public const string Schema = "pronunciation";

    public DbSet<ClipRow> Clips => Set<ClipRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<ClipRow>(clip =>
        {
            clip.ToTable("clips");
            clip.HasKey(c => new { c.LexemeId, c.Voice });
            clip.Property(c => c.LexemeId).HasColumnName("lexeme_id");
            clip.Property(c => c.Voice).HasColumnName("voice").HasMaxLength(32);
            clip.Property(c => c.EngineVersion).HasColumnName("engine_version").HasMaxLength(64).IsRequired();
            clip.Property(c => c.Text).HasColumnName("text").HasMaxLength(64).IsRequired();
            clip.Property(c => c.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            clip.Property(c => c.Path).HasColumnName("path").HasMaxLength(256);
            clip.Property(c => c.Sha256).HasColumnName("sha256").HasMaxLength(64);
            clip.Property(c => c.Bytes).HasColumnName("bytes");
            clip.Property(c => c.RequestedBy).HasColumnName("requested_by").HasMaxLength(16).IsRequired();
            clip.Property(c => c.CreatedAt).HasColumnName("created_at");
            clip.Property(c => c.RenderedAt).HasColumnName("rendered_at");
            clip.Property(c => c.Attempts).HasColumnName("attempts");
            clip.Property(c => c.LastError).HasColumnName("last_error").HasMaxLength(256);
            clip.HasIndex(c => new { c.Status, c.EngineVersion }).HasDatabaseName("ix_clips_status_engine");
        });
    }
}

public sealed class ClipRow
{
    public Guid LexemeId { get; set; }

    public string Voice { get; set; } = string.Empty;

    public string EngineVersion { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Path { get; set; }

    public string? Sha256 { get; set; }

    public long Bytes { get; set; }

    public string RequestedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RenderedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
