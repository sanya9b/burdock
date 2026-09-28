using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Learning;

/// <summary>What the learner is called inside the app (decision T10): an auto-generated, editable nickname; no e-mail, no real name required.</summary>
public sealed record LearnerProfile(string Nickname)
{
    public const int MinNicknameLength = 2;
    public const int MaxNicknameLength = 24;

    /// <summary>Trims and validates the nickname: 2–24 visible characters, no control characters.</summary>
    public static Result<LearnerProfile> Create(string? nickname)
    {
        var trimmed = (nickname ?? string.Empty).Trim();
        if (trimmed.Length is < MinNicknameLength or > MaxNicknameLength)
        {
            return DomainError.Validation("profile.nickname.length", "Errors_Profile_NicknameLength");
        }

        if (trimmed.Any(char.IsControl))
        {
            return DomainError.Validation("profile.nickname.characters", "Errors_Profile_NicknameCharacters");
        }

        return new LearnerProfile(trimmed);
    }
}
