using System.Globalization;
using System.Text;

namespace Burdock.Domain.Learning;

/// <summary>Result of checking a typed answer against accepted forms.</summary>
public readonly record struct AnswerCheck(bool Correct, bool NearMiss, string Matched)
{
    public static AnswerCheck Exact(string matched) => new(true, false, matched);

    public static AnswerCheck Typo(string matched) => new(true, true, matched);

    public static AnswerCheck Wrong => new(false, false, string.Empty);
}

/// <summary>
/// Typed-recall checking (spec 06 §3.6): case-fold, trim, collapse whitespace, unify apostrophes (U+0027, U+2019, U+02BC),
/// strip diacritics, treat hyphen and space as equal, accept listed spelling variants, and forgive one edit
/// (Damerau-Levenshtein ≤ 1) for words of at least <see cref="MinLengthForTypo"/> letters.
/// </summary>
public static class AnswerNormalizer
{
    /// <summary>learning.typed.minLengthForTypo</summary>
    public const int MinLengthForTypo = 5;

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decomposed = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousSpace = false;
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var c = ch switch
            {
                '’' or 'ʼ' or '‘' or '´' or '`' => '\'',
                '-' or '‐' or '‑' or '–' or '_' => ' ',
                _ => ch,
            };

            if (char.IsWhiteSpace(c))
            {
                if (!previousSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                previousSpace = true;
                continue;
            }

            previousSpace = false;
            builder.Append(c);
        }

        return builder.ToString().TrimEnd();
    }

    public static AnswerCheck Check(string? typed, IReadOnlyCollection<string> acceptedForms)
    {
        ArgumentNullException.ThrowIfNull(acceptedForms);
        var answer = Normalize(typed);
        if (answer.Length == 0)
        {
            return AnswerCheck.Wrong;
        }

        var normalizedForms = acceptedForms.Select(f => (Original: f, Normalized: Normalize(f))).Where(f => f.Normalized.Length > 0).ToArray();
        foreach (var form in normalizedForms)
        {
            if (string.Equals(form.Normalized, answer, StringComparison.Ordinal))
            {
                return AnswerCheck.Exact(form.Original);
            }
        }

        foreach (var form in normalizedForms)
        {
            if (form.Normalized.Length >= MinLengthForTypo && DamerauLevenshtein(form.Normalized, answer) == 1)
            {
                return AnswerCheck.Typo(form.Original);
            }
        }

        return AnswerCheck.Wrong;
    }

    /// <summary>Optimal string alignment distance (adjacent transposition counts as one edit).</summary>
    public static int DamerauLevenshtein(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }
}
