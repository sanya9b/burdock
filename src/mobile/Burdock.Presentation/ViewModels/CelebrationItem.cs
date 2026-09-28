namespace Burdock.Presentation.ViewModels;

/// <summary>One celebration to show on the RewardCard overlay (spec 07 §3.8.1): level 2 = joy, 3 = party card with confetti, 4 = the big one.</summary>
public sealed record CelebrationItem(int Level, string Eyebrow, string Title, string Body, string Reward, string RewardIcon, string Mascot, string Button)
{
    public bool HasBody => Body.Length > 0;

    public bool HasReward => Reward.Length > 0;
}
