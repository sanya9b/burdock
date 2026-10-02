using System.Security.Cryptography;
using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Identity.Application;
using Burdock.Server.Modules.Identity.Domain;

namespace Burdock.Server.Modules.Identity.Infrastructure;

/// <summary>EF Core stores over one scoped <see cref="IdentityDbContext"/>; <see cref="CommitAsync"/> is the unit of work.</summary>
public sealed class EfIdentityStores(IdentityDbContext db) : IAccountStore, IInstallationStore, IIdentityUnitOfWork
{
    public async Task<UserAccount?> GetAsync(LearnerId id, CancellationToken cancellationToken)
    {
        var row = await db.Users.FindAsync([id.Value], cancellationToken).ConfigureAwait(false);
        return row is null ? null : UserAccount.Restore(LearnerId.From(row.Id), row.Nickname, Enum.Parse<AccountStatus>(row.Status), row.CreatedAt, row.LastSeenAt, row.PurgeAt);
    }

    public Task AddAsync(UserAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        db.Users.Add(new UserRow { Id = account.Id.Value, Nickname = account.Nickname, Status = account.Status.ToString(), CreatedAt = account.CreatedAt, LastSeenAt = account.LastSeenAt, PurgeAt = account.PurgeAt });
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(UserAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        var row = await db.Users.FindAsync([account.Id.Value], cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Account {account.Id} is not stored.");
        row.Nickname = account.Nickname;
        row.Status = account.Status.ToString();
        row.LastSeenAt = account.LastSeenAt;
        row.PurgeAt = account.PurgeAt;
    }

    public async Task<Installation?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Installations.FindAsync([id], cancellationToken).ConfigureAwait(false);
        return row is null ? null : Installation.Restore(row.Id, LearnerId.From(row.UserId), row.SecretHash, Enum.Parse<ClientPlatform>(row.Platform), row.AppVersion, row.CreatedAt, row.LastSeenAt, row.RevokedAt);
    }

    public Task AddAsync(Installation installation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installation);
        db.Installations.Add(new InstallationRow
        {
            Id = installation.Id,
            UserId = installation.UserId.Value,
            SecretHash = installation.SecretHash,
            Platform = installation.Platform.ToString(),
            AppVersion = installation.AppVersion,
            CreatedAt = installation.CreatedAt,
            LastSeenAt = installation.LastSeenAt,
            RevokedAt = installation.RevokedAt,
        });
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Installation installation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installation);
        var row = await db.Installations.FindAsync([installation.Id], cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Installation {installation.Id} is not stored.");
        row.AppVersion = installation.AppVersion;
        row.LastSeenAt = installation.LastSeenAt;
        row.RevokedAt = installation.RevokedAt;
    }

    public Task CommitAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>Cryptographic randomness for nicknames (never <see cref="Random"/>: tags must not be predictable from earlier ones).</summary>
public sealed class CryptoRandomSource : IRandomSource
{
    public int Next(int minInclusive, int maxExclusive) => RandomNumberGenerator.GetInt32(minInclusive, maxExclusive);
}
