using Burdock.Application.Ports;
using Burdock.Contracts.Content;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Infrastructure.Content;

/// <summary>Maps the transport document (Contracts) into Domain entries; the only place that knows both shapes.</summary>
public static class ContentPackMapper
{
    public static Result<ImportedContentPack> ToDomain(ContentPackDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion > ContentPackDocument.CurrentSchemaVersion)
        {
            return DomainError.Validation("content.schemaTooNew", "Errors_Content_SchemaTooNew");
        }

        var pair = LanguagePair.Parse(document.Pair);
        if (pair.IsFailure)
        {
            return pair.Error;
        }

        var entries = new List<WordEntry>(document.Entries.Count);
        foreach (var e in document.Entries)
        {
            if (!Enum.TryParse<PartOfSpeech>(e.PartOfSpeech, ignoreCase: true, out var pos))
            {
                return DomainError.Validation("content.pos", "Errors_Content_InvalidPartOfSpeech", new Dictionary<string, string> { ["lemma"] = e.Lemma, ["pos"] = e.PartOfSpeech });
            }

            if (!Enum.TryParse<CefrLevel>(e.Level, ignoreCase: true, out var level))
            {
                return DomainError.Validation("content.level", "Errors_Content_InvalidLevel", new Dictionary<string, string> { ["lemma"] = e.Lemma, ["level"] = e.Level });
            }

            var lexeme = new Lexeme(LexemeId.From(e.Id), pair.Value, e.Lemma, pos, level, e.Topics.Select(TopicId.From).ToArray(), e.FrequencyBand);
            entries.Add(new WordEntry(lexeme, e.Translation, e.AlternativeTranslations, e.AcceptedForms, e.IpaGeneralAmerican, e.IpaReceivedPronunciation, e.ExampleTarget, e.ExampleNative));
        }

        return new ImportedContentPack(document.PackId, pair.Value, document.Version, entries);
    }
}
