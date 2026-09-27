namespace Burdock.Domain.Learning;

/// <summary>FSRS memory state of a card: stability in days and difficulty on the 1-10 scale (spec 06 §3.3).</summary>
public readonly record struct MemoryState(double Stability, double Difficulty)
{
    public static MemoryState Create(double stability, double difficulty)
    {
        if (stability <= 0 || double.IsNaN(stability) || double.IsInfinity(stability))
        {
            throw new ArgumentOutOfRangeException(nameof(stability), stability, "Stability must be a positive number of days.");
        }

        if (difficulty is < 1 or > 10 || double.IsNaN(difficulty))
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Difficulty is on the 1-10 scale.");
        }

        return new MemoryState(stability, difficulty);
    }
}
