using SQLite;

namespace Burdock.Infrastructure.Persistence;

/// <summary>Storage rows for sqlite-net. Dates are stored as UTC ticks; lists as JSON or CSV. Never used outside Infrastructure.</summary>
[Table("word_entries")]
public sealed class WordEntryRow
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public string Pair { get; set; } = string.Empty;

    public string Lemma { get; set; } = string.Empty;

    public int PartOfSpeech { get; set; }

    [Indexed]
    public int Level { get; set; }

    /// <summary>Comma-separated topic slugs, each wrapped in commas for LIKE matching: ",travel,city,".</summary>
    public string Topics { get; set; } = string.Empty;

    public string Translation { get; set; } = string.Empty;

    public string AlternativeTranslationsJson { get; set; } = "[]";

    public string AcceptedFormsJson { get; set; } = "[]";

    public string? IpaGa { get; set; }

    public string? IpaRp { get; set; }

    public string? ExampleTarget { get; set; }

    public string? ExampleNative { get; set; }

    public int? FrequencyBand { get; set; }

    [Indexed]
    public string PackId { get; set; } = string.Empty;
}

[Table("content_packs")]
public sealed class ContentPackRow
{
    [PrimaryKey]
    public string PackId { get; set; } = string.Empty;

    public int Version { get; set; }

    public long ImportedAtTicks { get; set; }
}

[Table("learner_cards")]
public sealed class LearnerCardRow
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid LearnerId { get; set; }

    [Indexed]
    public Guid LexemeId { get; set; }

    public long CreatedAtTicks { get; set; }

    public int State { get; set; }

    public double? Stability { get; set; }

    public double? Difficulty { get; set; }

    public int? Step { get; set; }

    public long? DueTicks { get; set; }

    public long? LastReviewTicks { get; set; }

    public long? IntroducedAtTicks { get; set; }

    public long? LearnedAtTicks { get; set; }

    public int Reps { get; set; }

    public int Lapses { get; set; }

    public int ScheduledExposures { get; set; }

    public int ConfirmedExposures { get; set; }

    public int RecallSuccesses { get; set; }

    /// <summary>Learning days as yyyy-MM-dd joined by commas.</summary>
    public string ExposureDays { get; set; } = string.Empty;

    public string RecallSuccessDays { get; set; } = string.Empty;
}

[Table("review_log")]
public sealed class ReviewLogRow
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid LearnerId { get; set; }

    public Guid CardId { get; set; }

    public Guid LexemeId { get; set; }

    public int Rating { get; set; }

    public int Mode { get; set; }

    [Indexed]
    public long ReviewedAtTicks { get; set; }

    public long ResponseTimeTicks { get; set; }

    public double? StabilityAfter { get; set; }

    public double? DifficultyAfter { get; set; }

    public long DueAfterTicks { get; set; }

    public bool Synced { get; set; }
}

[Table("exposures")]
public sealed class ExposureRow
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid LearnerId { get; set; }

    [Indexed]
    public Guid LexemeId { get; set; }

    public int Surface { get; set; }

    public int Kind { get; set; }

    public long AtTicks { get; set; }

    public bool Synced { get; set; }
}
