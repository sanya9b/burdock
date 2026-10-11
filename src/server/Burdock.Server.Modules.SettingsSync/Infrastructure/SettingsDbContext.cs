using Microsoft.EntityFrameworkCore;

namespace Burdock.Server.Modules.SettingsSync.Infrastructure;

/// <summary>Schema <c>settings</c> (spec 09 §3.6): one row per (learner, key) plus a document header with the ETag.</summary>
public sealed class SettingsDbContext(DbContextOptions<SettingsDbContext> options) : DbContext(options)
{
    public const string Schema = "settings";

    public DbSet<SettingsEntryRow> Entries => Set<SettingsEntryRow>();

    public DbSet<SettingsDocumentRow> Documents => Set<SettingsDocumentRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<SettingsEntryRow>(e =>
        {
            e.ToTable("entries");
            e.HasKey(x => new { x.UserId, x.Key });
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Key).HasColumnName("key").HasMaxLength(64);
            e.Property(x => x.Value).HasColumnName("value").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.ClientId).HasColumnName("client_id").HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<SettingsDocumentRow>(d =>
        {
            d.ToTable("documents");
            d.HasKey(x => x.UserId);
            d.Property(x => x.UserId).HasColumnName("user_id");
            d.Property(x => x.SchemaVersion).HasColumnName("schema_version");
            d.Property(x => x.Etag).HasColumnName("etag").HasMaxLength(32).IsRequired();
            d.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
    }
}

public sealed class SettingsEntryRow
{
    public Guid UserId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = "null";

    public DateTimeOffset UpdatedAt { get; set; }

    public string ClientId { get; set; } = string.Empty;
}

public sealed class SettingsDocumentRow
{
    public Guid UserId { get; set; }

    public int SchemaVersion { get; set; }

    public string Etag { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
