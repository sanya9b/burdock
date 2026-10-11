using Microsoft.EntityFrameworkCore;

namespace Burdock.Server.Modules.LearnerProgress.Infrastructure;

/// <summary>
/// Schema <c>progress</c> (spec 09 §3.5, §3.11): the event log and the per-learner sequence. Monthly partitioning of
/// <c>events</c> arrives with a later raw-SQL migration once volumes justify it.
/// </summary>
public sealed class ProgressDbContext(DbContextOptions<ProgressDbContext> options) : DbContext(options)
{
    public const string Schema = "progress";

    public DbSet<EventRow> Events => Set<EventRow>();

    public DbSet<UserSequenceRow> Sequences => Set<UserSequenceRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<EventRow>(e =>
        {
            e.ToTable("events");
            e.HasKey(x => new { x.UserId, x.Id });
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ServerSeq).HasColumnName("server_seq");
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(32).IsRequired();
            e.Property(x => x.SchemaVersion).HasColumnName("schema_version");
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.Property(x => x.LocalDate).HasColumnName("local_date");
            e.Property(x => x.InstallationId).HasColumnName("installation_id");
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.ReceivedAt).HasColumnName("received_at");
            e.HasIndex(x => new { x.UserId, x.ServerSeq }).IsUnique().HasDatabaseName("ux_events_user_seq");
            e.HasIndex(x => new { x.UserId, x.LocalDate }).HasDatabaseName("ix_events_user_local_date");
        });

        modelBuilder.Entity<UserSequenceRow>(s =>
        {
            s.ToTable("user_seq");
            s.HasKey(x => x.UserId);
            s.Property(x => x.UserId).HasColumnName("user_id");
            s.Property(x => x.LastSeq).HasColumnName("last_seq");
        });
    }
}

public sealed class EventRow
{
    public Guid UserId { get; set; }

    public Guid Id { get; set; }

    public long ServerSeq { get; set; }

    public string Type { get; set; } = string.Empty;

    public int SchemaVersion { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateOnly LocalDate { get; set; }

    public Guid? InstallationId { get; set; }

    public string Payload { get; set; } = "{}";

    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class UserSequenceRow
{
    public Guid UserId { get; set; }

    public long LastSeq { get; set; }
}
