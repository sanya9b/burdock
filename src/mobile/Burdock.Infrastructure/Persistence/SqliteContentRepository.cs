using System.Globalization;
using System.Text.Json;
using Burdock.Application.Ports;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Infrastructure.Persistence;

public sealed class SqliteContentRepository(BurdockDatabase database) : IContentRepository
{
    /// <summary>Stays well under SQLite's default limit of 999 bound variables per statement.</summary>
    private const int IdChunkSize = 400;

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
        if (levels.Count == 0)
        {
            return [];
        }

        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        // Filters run in SQL: with ~10k bundled entries, loading every row of the pair and filtering in memory cost seconds on the day plan.
        var args = new List<object> { pair.ToString() };
        var sql = new System.Text.StringBuilder("SELECT * FROM word_entries WHERE Pair = ?");
        sql.Append(" AND Level IN (").Append(string.Join(", ", levels.Select(_ => "?"))).Append(')');
        args.AddRange(levels.Select(l => (object)(int)l));
        if (topics.Count > 0)
        {
            sql.Append(" AND (").Append(string.Join(" OR ", topics.Select(_ => "Topics LIKE ?"))).Append(')');
            args.AddRange(topics.Select(t => (object)("%," + t.Slug + ",%")));
        }

        sql.Append(" ORDER BY Lemma");
        var rows = await db.QueryAsync<WordEntryRow>(sql.ToString(), [.. args]).ConfigureAwait(false);
        return rows.Select(ToEntry).ToArray();
    }

    public async Task<IReadOnlyList<WordEntry>> ListByIdsAsync(IReadOnlyCollection<LexemeId> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return [];
        }

        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<WordEntry>(ids.Count);
        foreach (var chunk in ids.Select(id => (object)id.Value).Chunk(IdChunkSize))
        {
            var sql = "SELECT * FROM word_entries WHERE Id IN (" + string.Join(", ", chunk.Select(_ => "?")) + ")";
            var rows = await db.QueryAsync<WordEntryRow>(sql, chunk).ConfigureAwait(false);
            entries.AddRange(rows.Select(ToEntry));
        }

        return entries;
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

    public async Task<IReadOnlyDictionary<string, int>> GetImportedPackVersionsAsync(CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.Table<ContentPackRow>().ToListAsync().ConfigureAwait(false);
        return rows.ToDictionary(r => r.PackId, r => r.Version, StringComparer.Ordinal);
    }

    public async Task ImportPackAsync(ImportedContentPack pack, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = pack.Entries.Select(e => ToRow(e, pack.PackId)).ToList();
        await db.RunInTransactionAsync(conn =>
        {
            conn.Execute("DELETE FROM word_entries WHERE PackId = ?", pack.PackId);
            // A lexeme with several topics ships in each topic pack (spec 08 §3.10); the client merges by id.
            conn.InsertAll(rows, "OR REPLACE");
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
