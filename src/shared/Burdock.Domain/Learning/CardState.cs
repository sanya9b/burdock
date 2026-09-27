namespace Burdock.Domain.Learning;

/// <summary>Lifecycle of a word for one learner (spec 06 §3.2 state machine).</summary>
public enum CardState
{
    /// <summary>Never shown to the learner.</summary>
    New,

    /// <summary>Seen passively at least three times on two distinct days, or opened in the app; not yet graded.</summary>
    Introduced,

    /// <summary>Being learned with short intervals.</summary>
    Learning,

    /// <summary>In long-term review.</summary>
    Review,

    /// <summary>Lapsed from review and being relearned.</summary>
    Relearning,

    /// <summary>"Вивчено": three correct recalls on two or more days and stability of at least 21 days.</summary>
    Learned,

    /// <summary>The learner claimed to know it ("Знаю"); verified later by typed recall.</summary>
    Known,

    /// <summary>Excluded from all surfaces by the learner.</summary>
    Suspended,
}

/// <summary>Grade given to a card after an active-recall exercise; maps onto FSRS-6 ratings (ADR 0008).</summary>
public enum Rating
{
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4,
}
