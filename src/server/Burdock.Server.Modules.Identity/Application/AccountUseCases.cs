using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Identity.Domain;
using Burdock.Server.Shared.Time;

namespace Burdock.Server.Modules.Identity.Application;

/// <summary>The installation grant (spec 09 §3.3.1): id + secret → the account the tokens are issued for.</summary>
public sealed class AuthenticateInstallationUseCase(IAccountStore accounts, IInstallationStore installations, IIdentityUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result<UserAccount>> ExecuteAsync(Guid installationId, string? secret, string? appVersion, CancellationToken cancellationToken)
    {
        var parsed = InstallationSecret.Parse(secret);
        if (installationId == Guid.Empty || parsed.IsFailure)
        {
            return Unknown();
        }

        var installation = await installations.GetAsync(installationId, cancellationToken).ConfigureAwait(false);
        if (installation is null || !installation.IsActive || !installation.Matches(parsed.Value))
        {
            return Unknown();
        }

        var account = await accounts.GetAsync(installation.UserId, cancellationToken).ConfigureAwait(false);
        if (account is null || !account.IsActive)
        {
            return Unknown();
        }

        var now = clock.UtcNow;
        installation.Seen(appVersion, now);
        account.Touch(now);
        await installations.UpdateAsync(installation, cancellationToken).ConfigureAwait(false);
        await accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(account);
    }

    /// <summary>One answer for every failure so the response does not reveal which installations exist.</summary>
    private static Result<UserAccount> Unknown() => Result.Failure<UserAccount>(DomainError.Forbidden("installation_unknown", "Errors_Identity_InstallationUnknown"));
}

public sealed class GetAccountUseCase(IAccountStore accounts)
{
    public async Task<Result<UserAccount>> ExecuteAsync(LearnerId id, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return account is null
            ? Result.Failure<UserAccount>(DomainError.NotFound("identity.account_not_found", "Errors_Identity_AccountNotFound"))
            : Result.Success(account);
    }
}

/// <summary>PATCH /v1/identity/me: the nickname is the only editable field of an anonymous account.</summary>
public sealed class RenameAccountUseCase(IAccountStore accounts, IIdentityUnitOfWork unitOfWork)
{
    public async Task<Result<UserAccount>> ExecuteAsync(LearnerId id, string? nickname, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return Result.Failure<UserAccount>(DomainError.NotFound("identity.account_not_found", "Errors_Identity_AccountNotFound"));
        }

        var renamed = account.Rename(nickname);
        if (renamed.IsFailure)
        {
            return Result.Failure<UserAccount>(renamed.Error);
        }

        await accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(account);
    }
}
