using Microsoft.EntityFrameworkCore;

namespace Burdock.Server.Modules.Content.Infrastructure;

/// <summary>Schema <c>content</c> (spec 09 §3.4): published pack versions; reports arrive with FR-09-9.</summary>
public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options)
{
    public const string Schema = "content";

    public DbSet<PackRow> Packs => Set<PackRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<PackRow>(pack =>
        {
            pack.ToTable("packs");
            pack.HasKey(p => new { p.PackId, p.Version });
            pack.Property(p => p.PackId).HasColumnName("id").HasMaxLength(64);
            pack.Property(p => p.Version).HasColumnName("version");
            pack.Property(p => p.Pair).HasColumnName("pair").HasMaxLength(8).IsRequired();
            pack.Property(p => p.Level).HasColumnName("level").HasMaxLength(16).IsRequired();
            pack.Property(p => p.Topic).HasColumnName("topic").HasMaxLength(32).IsRequired();
            pack.Property(p => p.Sha256).HasColumnName("sha256").HasMaxLength(64).IsRequired();
            pack.Property(p => p.SizeBytes).HasColumnName("size_bytes");
            pack.Property(p => p.ObjectKey).HasColumnName("object_key").HasMaxLength(256).IsRequired();
            pack.Property(p => p.MinAppVersion).HasColumnName("min_app_version").HasMaxLength(32).IsRequired();
            pack.Property(p => p.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            pack.Property(p => p.PublishedAt).HasColumnName("published_at");
            pack.Property(p => p.DeprecatedAfter).HasColumnName("deprecated_after");
            pack.Property(p => p.AttributionsJson).HasColumnName("attributions").HasColumnType("jsonb").IsRequired();
            pack.HasIndex(p => p.Status).HasDatabaseName("ix_packs_status");
            pack.HasIndex(p => new { p.Pair, p.Status }).HasDatabaseName("ix_packs_pair_status");
        });
    }
}

public sealed class PackRow
{
    public string PackId { get; set; } = string.Empty;

    public int Version { get; set; }

    public string Pair { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string ObjectKey { get; set; } = string.Empty;

    public string MinAppVersion { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset PublishedAt { get; set; }

    public DateOnly? DeprecatedAfter { get; set; }

    public string AttributionsJson { get; set; } = "[]";
}
