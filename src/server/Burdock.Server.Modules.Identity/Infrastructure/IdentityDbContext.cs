using Microsoft.EntityFrameworkCore;

namespace Burdock.Server.Modules.Identity.Infrastructure;

/// <summary>Schema <c>identity</c> (spec 09 §3.3.4): users, installations and the OpenIddict stores; migrations history lives in the same schema.</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<UserRow> Users => Set<UserRow>();

    public DbSet<InstallationRow> Installations => Set<InstallationRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<UserRow>(user =>
        {
            user.ToTable("users");
            user.HasKey(u => u.Id);
            user.Property(u => u.Id).HasColumnName("id");
            user.Property(u => u.Nickname).HasColumnName("nickname").HasMaxLength(64).IsRequired();
            user.Property(u => u.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            user.Property(u => u.CreatedAt).HasColumnName("created_at");
            user.Property(u => u.LastSeenAt).HasColumnName("last_seen_at");
            user.Property(u => u.PurgeAt).HasColumnName("purge_at");
            user.HasIndex(u => u.LastSeenAt).HasDatabaseName("ix_users_last_seen_at");
            user.HasIndex(u => new { u.Status, u.PurgeAt }).HasDatabaseName("ix_users_status_purge_at");
        });

        modelBuilder.Entity<InstallationRow>(installation =>
        {
            installation.ToTable("installations");
            installation.HasKey(i => i.Id);
            installation.Property(i => i.Id).HasColumnName("id");
            installation.Property(i => i.UserId).HasColumnName("user_id");
            installation.Property(i => i.SecretHash).HasColumnName("secret_hash").HasMaxLength(32).IsRequired();
            installation.Property(i => i.Platform).HasColumnName("platform").HasMaxLength(16).IsRequired();
            installation.Property(i => i.AppVersion).HasColumnName("app_version").HasMaxLength(32).IsRequired();
            installation.Property(i => i.CreatedAt).HasColumnName("created_at");
            installation.Property(i => i.LastSeenAt).HasColumnName("last_seen_at");
            installation.Property(i => i.RevokedAt).HasColumnName("revoked_at");
            installation.HasIndex(i => i.UserId).HasDatabaseName("ix_installations_user_id");
            installation.HasOne<UserRow>().WithMany().HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.UseOpenIddict();
    }
}

/// <summary>Persistence shape of <see cref="Domain.UserAccount"/>; the domain object stays free of EF attributes.</summary>
public sealed class UserRow
{
    public Guid Id { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? PurgeAt { get; set; }
}

public sealed class InstallationRow
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public byte[] SecretHash { get; set; } = [];

    public string Platform { get; set; } = string.Empty;

    public string AppVersion { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
