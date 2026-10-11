namespace Burdock.Domain.Entitlements;

/// <summary>
/// The subscription state machine shared by the server (spec 09 §3.8) and the client (spec 11): what the learner may use.
/// Free is the baseline after the trial; Trial, Active, Grace and BillingRetry unlock Pro content; Expired and Revoked fall back to Free rules.
/// </summary>
public enum EntitlementState
{
    Free,
    Trial,
    Active,
    Grace,
    BillingRetry,
    Expired,
    Revoked,
}

public static class EntitlementStateExtensions
{
    /// <summary>States that unlock every level and category (spec 11 §3.2, spec 09 §3.4).</summary>
    public static bool UnlocksPro(this EntitlementState state) => state is EntitlementState.Trial or EntitlementState.Active or EntitlementState.Grace or EntitlementState.BillingRetry;
}
