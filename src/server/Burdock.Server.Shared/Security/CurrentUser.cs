using System.Security.Claims;
using Burdock.Domain.SharedKernel;

namespace Burdock.Server.Shared.Security;

/// <summary>Resources are always addressed as /me (spec 09 §3.2): the learner id comes from the <c>sub</c> claim and never from the path.</summary>
public static class CurrentUser
{
    public const string SubjectClaim = "sub";

    public static bool TryGetLearnerId(ClaimsPrincipal principal, out LearnerId learnerId)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var raw = principal.FindFirst(SubjectClaim)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(raw, out var id) && id != Guid.Empty)
        {
            learnerId = LearnerId.From(id);
            return true;
        }

        learnerId = default;
        return false;
    }
}

/// <summary>Authorization policy and rate-limit policy names shared by the host and the modules (spec 09 §3.2.1).</summary>
public static class Policies
{
    public const string User = "user";

    public static class RateLimits
    {
        public const string AnonIp = "anon-ip";
        public const string IdentityCreate = "identity-create";
        public const string Token = "token";
        public const string User = "user";
        public const string UserWrite = "user-write";
    }
}
