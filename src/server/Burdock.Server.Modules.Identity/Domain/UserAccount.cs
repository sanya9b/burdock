using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;

namespace Burdock.Server.Modules.Identity.Domain;

public enum AccountStatus
{
    Active,
    PendingDeletion,
}

/// <summary>
/// The anonymous account (spec 09 §3.3, decision T10): a nickname and timestamps, nothing personal. Deletion is soft
/// (PendingDeletion with a purge date) so the learner can change their mind within the retention window.
/// </summary>
public sealed class UserAccount
{
    private UserAccount(LearnerId id, string nickname, AccountStatus status, DateTimeOffset createdAt, DateTimeOffset lastSeenAt, DateTimeOffset? purgeAt)
    {
        Id = id;
        Nickname = nickname;
        Status = status;
        CreatedAt = createdAt;
        LastSeenAt = lastSeenAt;
        PurgeAt = purgeAt;
    }

    public LearnerId Id { get; }

    public string Nickname { get; private set; }

    public AccountStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset? PurgeAt { get; private set; }

    public bool IsActive => Status == AccountStatus.Active;

    public static UserAccount Register(LearnerId id, string nickname, DateTimeOffset now)
    {
        var profile = LearnerProfile.Create(nickname);
        return new UserAccount(id, profile.IsSuccess ? profile.Value.Nickname : nickname.Trim(), AccountStatus.Active, now, now, null);
    }

    /// <summary>Rehydration from storage; no rules run.</summary>
    public static UserAccount Restore(LearnerId id, string nickname, AccountStatus status, DateTimeOffset createdAt, DateTimeOffset lastSeenAt, DateTimeOffset? purgeAt)
        => new(id, nickname, status, createdAt, lastSeenAt, purgeAt);

    /// <summary>Same rules as the client's profile (shared Domain): 2–24 visible characters, no control characters.</summary>
    public Result Rename(string? nickname)
    {
        var profile = LearnerProfile.Create(nickname);
        if (profile.IsFailure)
        {
            return Result.Failure(profile.Error);
        }

        Nickname = profile.Value.Nickname;
        return Result.Success();
    }

    public void Touch(DateTimeOffset now)
    {
        if (now > LastSeenAt)
        {
            LastSeenAt = now;
        }
    }
}
