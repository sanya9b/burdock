using System.Globalization;
using System.Text.Json;
using Burdock.Application.Ports;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Infrastructure.Persistence;

public sealed class SqliteContentRepository(BurdockDatabase database) : IContentRepository
{
    public async Task<WordEntry?> GetAsync(LexemeId id, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.FindAsync<WordEntryRow>(id.Value).ConfigureAwait(false);
        return row is null ? null : ToEntry(row);
    }

    public async Task<IReadOnlyList<WordEntry>> ListAsync(LanguagePair pair, IReadOnlyCollection<CefrLevel> levels, IReadOnlyCollection<TopicId> topics, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(topics);
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var pairKey = pair.ToString();
        var levelSet = levels.Select(l => (int)l).ToHashSet();
        var rows = await db.Table<WordEntryRow>().Where(r => r.Pair == pairKey).OrderBy(r => r.Lemma).ToListAsync().ConfigureAwait(false);
        var topicKeys = topics.Select(t => "," + t.Slug + ",").ToArray();
        return rows
            .Where(r => levelSet.Contains(r.Level) && (topicKeys.Length == 0 || topicKeys.Any(k => r.Topics.Contains(k, StringComparison.Ordinal))))
            .Select(ToEntry)
            .ToArray();
    }

    public async Task<int> CountAsync(LanguagePair pair, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var pairKey = pair.ToString();
        return await db.Table<WordEntryRow>().Where(r => r.Pair == pairKey).CountAsync().ConfigureAwait(false);
    }

    public async Task<int?> GetImportedPackVersionAsync(string packId, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.FindAsync<ContentPackRow>(packId).ConfigureAwait(false);
        return row?.Version;
    }

    public async Task ImportPackAsync(ImportedContentPack pack, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = pack.Entries.Select(e => ToRow(e, pack.PackId)).ToList();
        await db.RunInTransactionAsync(conn =>
        {
            conn.Execute("DELETE FROM word_entries WHERE PackId = ?", pack.PackId);
            conn.InsertAll(rows);
            conn.InsertOrReplace(new ContentPackRow { PackId = pack.PackId, Version = pack.Version, ImportedAtTicks = DateTimeOffset.UtcNow.UtcTicks });
        }).ConfigureAwait(false);
    }

    private static WordEntryRow ToRow(WordEntry entry, string packId) => new()
    {
        Id = entry.Id.Value,
        Pair = entry.Lexeme.Pair.ToString(),
        Lemma = entry.Lexeme.Lemma,
        PartOfSpeech = (int)entry.Lexeme.PartOfSpeech,
        Level = (int)entry.Lexeme.Level,
        Topics = "," + string.Join(",", entry.Lexeme.Topics.Select(t => t.Slug)) + ",",
        Translation = entry.Translation,
        AlternativeTranslationsJson = JsonSerializer.Serialize(entry.AlternativeTranslations, StringListJsonContext.Default.IReadOnlyListString),
        AcceptedFormsJson = JsonSerializer.Serialize(entry.AcceptedForms, StringListJsonContext.Default.IReadOnlyListString),
        IpaGa = entry.IpaGeneralAmerican,
        IpaRp = entry.IpaReceivedPronunciation,
        ExampleTarget = entry.ExampleTarget,
        ExampleNative = entry.ExampleNative,
        FrequencyBand = entry.Lexeme.FrequencyBand,
        PackId = packId,
    };

    private static WordEntry ToEntry(WordEntryRow row)
    {
        var pair = LanguagePair.Parse(row.Pair).Value;
        var topics = row.Topics.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(TopicId.From).ToArray();
        var lexeme = new Lexeme(LexemeId.From(row.Id), pair, row.Lemma, (PartOfSpeech)row.PartOfSpeech, (CefrLevel)row.Level, topics, row.FrequencyBand);
        return new WordEntry(
            lexeme,
            row.Translation,
            JsonSerializer.Deserialize(row.AlternativeTranslationsJson, StringListJsonContext.Default.IReadOnlyListString) ?? [],
            JsonSerializer.Deserialize(row.AcceptedFormsJson, StringListJsonContext.Default.IReadOnlyListString) ?? [],
            row.IpaGa,
            row.IpaRp,
            row.ExampleTarget,
            row.ExampleNative);
    }
}
