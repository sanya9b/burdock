using System.Text;
using Burdock.Domain.SharedKernel;

namespace Burdock.Server.Modules.Pronunciation.Domain;

/// <summary>
/// Text the engine is asked to pronounce (ADR 0020 §3): a word or a short phrase in the Latin alphabet, 1–64 characters,
/// with apostrophes, hyphens, periods and spaces allowed (a.m., call into question, rock 'n' roll). Whitespace is collapsed
/// and typographic apostrophes are normalised so the same word never renders twice under two spellings.
/// </summary>
public readonly record struct PronunciationText
{
    public const int MaxLength = 64;

    private PronunciationText(string value) => Value = value;

    public string Value { get; }

    public static Result<PronunciationText> Create(string? raw)
    {
        var text = Normalise(raw ?? string.Empty);
        if (text.Length == 0)
        {
            return Result.Failure<PronunciationText>(DomainError.Validation("pronunciation.text_empty", "Errors_Pronunciation_TextEmpty"));
        }

        if (text.Length > MaxLength)
        {
            return Result.Failure<PronunciationText>(DomainError.Validation(
                "pronunciation.text_too_long",
                "Errors_Pronunciation_TextTooLong",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["max"] = MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        }

        if (!text.Any(char.IsLetter) || !text.All(IsAllowed))
        {
            return Result.Failure<PronunciationText>(DomainError.Validation("pronunciation.text_invalid", "Errors_Pronunciation_TextInvalid"));
        }

        return Result.Success(new PronunciationText(text));
    }

    public override string ToString() => Value;

    private static string Normalise(string raw)
    {
        var builder = new StringBuilder(raw.Length);
        var pendingSpace = false;
        foreach (var c in raw.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c is '’' or 'ʼ' or '‘' or '´' ? '\'' : c);
        }

        return builder.ToString();
    }

    /// <summary>Latin letters (basic, Latin-1 and Latin Extended-A/B), digits and the punctuation a dictionary headword can carry.</summary>
    private static bool IsAllowed(char c)
        => char.IsLetter(c) ? c <= 'ɏ' : char.IsAsciiDigit(c) || c is ' ' or '\'' or '-' or '.' or ',' or '!' or '?' or '&';
}
