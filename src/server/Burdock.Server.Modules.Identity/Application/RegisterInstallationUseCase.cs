using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Identity.Domain;
using Burdock.Server.Shared.Time;

namespace Burdock.Server.Modules.Identity.Application;

public sealed record RegisterInstallationCommand(Guid InstallationId, string? Secret, string? Platform, string? AppVersion);

public sealed record RegisteredAccount(UserAccount Account, bool Created);

/// <summary>
/// POST /v1/identity/anonymous (spec 09 §3.3.1, FR-09-1): creates the anonymous account for a new installation, or
/// returns the existing one when the same id comes back with the right secret. A known id with another secret is a
/// conflict (`installation_conflict`); the client then generates a new installation.
/// </summary>
public sealed class RegisterInstallationUseCase(IAccountStore accounts, IInstallationStore installations, IIdentityUnitOfWork unitOfWork, IRandomSource random, IClock clock)
{
    public async Task<Result<RegisteredAccount>> ExecuteAsync(RegisterInstallationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.InstallationId == Guid.Empty)
        {
            return Result.Failure<RegisteredAccount>(DomainError.Validation("identity.installation_invalid", "Errors_Identity_InstallationInvalid"));
        }

        var secret = InstallationSecret.Parse(command.Secret);
        if (secret.IsFailure)
        {
            return Result.Failure<RegisteredAccount>(secret.Error);
        }

        var platform = Installation.ParsePlatform(command.Platform);
        if (platform.IsFailure)
        {
            return Result.Failure<RegisteredAccount>(platform.Error);
        }

        var now = clock.UtcNow;
        var existing = await installations.GetAsync(command.InstallationId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!existing.Matches(secret.Value))
            {
                return Result.Failure<RegisteredAccount>(DomainError.Conflict("installation_conflict", "Errors_Identity_InstallationConflict"));
            }

            var owner = await accounts.GetAsync(existing.UserId, cancellationToken).ConfigureAwait(false);
            if (owner is null || !owner.IsActive || !existing.IsActive)
            {
                return Result.Failure<RegisteredAccount>(DomainError.Conflict("installation_conflict", "Errors_Identity_InstallationConflict"));
            }

            existing.Seen(command.AppVersion, now);
            owner.Touch(now);
            await installations.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            await accounts.UpdateAsync(owner, cancellationToken).ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(new RegisteredAccount(owner, Created: false));
        }

        var account = UserAccount.Register(LearnerId.From(Guid.CreateVersion7(now)), Nicknames.Generate(random), now);
        var installation = Installation.Register(command.InstallationId, account.Id, secret.Value, platform.Value, command.AppVersion, now);
        if (installation.IsFailure)
        {
            return Result.Failure<RegisteredAccount>(installation.Error);
        }

        await accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
        await installations.AddAsync(installation.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new RegisteredAccount(account, Created: true));
    }
}
