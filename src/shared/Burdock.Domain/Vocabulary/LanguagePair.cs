using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Vocabulary;

/// <summary>ISO 639-1 language code such as "uk" or "en" (lower-case, two or three letters).</summary>
public readonly record struct LanguageCode
{
    private LanguageCode(string value) => Value = value;

    public string Value { get; }

    public static Result<LanguageCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DomainError.Validation("language.empty", "Errors_Language_Empty");
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length is < 2 or > 3 || !normalized.All(static c => c is >= 'a' and <= 'z'))
        {
            return DomainError.Validation("language.invalid", "Errors_Language_Invalid",
                new Dictionary<string, string> { ["value"] = value });
        }

        return new LanguageCode(normalized);
    }

    public static LanguageCode Ukrainian { get; } = new("uk");

    public static LanguageCode English { get; } = new("en");

    public override string ToString() => Value;
}

/// <summary>
/// The learner's native language and the language being learned. The domain never hard-codes
/// Ukrainian or English (decision Q11): every lexeme, pack and plan is scoped by a pair.
/// </summary>
public readonly record struct LanguagePair(LanguageCode Native, LanguageCode Target)
{
    /// <summary>The only pair shipped in v1: Ukrainian speakers learning English.</summary>
    public static LanguagePair UkrainianToEnglish { get; } = new(LanguageCode.Ukrainian, LanguageCode.English);

    /// <summary>Parses the canonical "native-target" form, for example "uk-en".</summary>
    public static Result<LanguagePair> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DomainError.Validation("languagePair.empty", "Errors_LanguagePair_Empty");
        }

        var parts = value.Split('-', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return DomainError.Validation("languagePair.invalid", "Errors_LanguagePair_Invalid",
                new Dictionary<string, string> { ["value"] = value });
        }

        return LanguageCode.Create(parts[0]).Bind(native =>
            LanguageCode.Create(parts[1]).Bind(target =>
                native == target
                    ? Result.Failure<LanguagePair>(DomainError.Validation("languagePair.same", "Errors_LanguagePair_SameLanguage"))
                    : Result.Success(new LanguagePair(native, target))));
    }

    /// <summary>Canonical identifier used in pack ids and API routes ("uk-en", T25).</summary>
    public override string ToString() => $"{Native}-{Target}";
}
