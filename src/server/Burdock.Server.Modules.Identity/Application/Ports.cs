using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Identity.Domain;

namespace Burdock.Server.Modules.Identity.Application;

public interface IAccountStore
{
    Task<UserAccount?> GetAsync(LearnerId id, CancellationToken cancellationToken);

    Task AddAsync(UserAccount account, CancellationToken cancellationToken);

    Task UpdateAsync(UserAccount account, CancellationToken cancellationToken);
}

public interface IInstallationStore
{
    Task<Installation?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Installation installation, CancellationToken cancellationToken);

    Task UpdateAsync(Installation installation, CancellationToken cancellationToken);
}

/// <summary>Commits the changes staged in the stores as one transaction (registration writes an account and an installation together).</summary>
public interface IIdentityUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken);
}

/// <summary>Randomness behind a port (CLAUDE.md §4): nickname words and tags come from a cryptographic source in production.</summary>
public interface IRandomSource
{
    int Next(int minInclusive, int maxExclusive);
}
