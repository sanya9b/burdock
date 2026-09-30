using Burdock.Application.Ports;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Infrastructure.Persistence;

namespace Burdock.Infrastructure.Tests.Persistence;

/// <summary>Pack import into the local SQLite store (spec 08 §3.10): a lexeme shipped in several topic packs is one row.</summary>
public sealed class SqliteContentRepositoryTests
{
    private static readonly LexemeId Receipt = LexemeId.From(Guid.Parse("0199a500-0000-7000-8000-000000000044"));

    [Fact]
    public async Task A_lexeme_present_in_two_topic_packs_is_stored_once_and_both_packs_count_as_imported()
    {
        var path = Path.Combine(Path.GetTempPath(), $"burdock-content-{Guid.NewGuid():N}.db3");
        try
        {
            var database = new BurdockDatabase(new DatabaseOptions(path));
            var repository = new SqliteContentRepository(database);
            var ct = TestContext.Current.CancellationToken;

            await repository.ImportPackAsync(Pack("uk-en.a2.shopping", Entry(["shopping", "food"])), ct);
            await repository.ImportPackAsync(Pack("uk-en.a2.food", Entry(["shopping", "food"])), ct);

            (await repository.CountAsync(LanguagePair.UkrainianToEnglish, ct)).ShouldBe(1);
            (await repository.GetImportedPackVersionAsync("uk-en.a2.shopping", ct)).ShouldBe(1);
            (await repository.GetImportedPackVersionAsync("uk-en.a2.food", ct)).ShouldBe(1);
            var stored = await repository.GetAsync(Receipt, ct);
            stored.ShouldNotBeNull();
            stored.Lexeme.Topics.Select(t => t.Slug).ShouldBe(["shopping", "food"]);

            var byTopic = await repository.ListAsync(LanguagePair.UkrainianToEnglish, [CefrLevel.A2], [TopicId.From("food")], ct);
            byTopic.Count.ShouldBe(1);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // the connection may still hold the file on Windows; the temp folder is cleaned by the OS
            }
        }
    }

    [Fact]
    public async Task ListAsync_filters_by_level_and_topic_in_the_query_and_orders_by_lemma()
    {
        var path = Path.Combine(Path.GetTempPath(), $"burdock-content-{Guid.NewGuid():N}.db3");
        try
        {
            var repository = new SqliteContentRepository(new BurdockDatabase(new DatabaseOptions(path)));
            var ct = TestContext.Current.CancellationToken;
            var pack = new ImportedContentPack("uk-en.mixed", LanguagePair.UkrainianToEnglish, 1,
            [
                Entry("00000000-0000-7000-8000-000000000001", "ticket", CefrLevel.A1, ["travel"]),
                Entry("00000000-0000-7000-8000-000000000002", "apple", CefrLevel.A1, ["food"]),
                Entry("00000000-0000-7000-8000-000000000003", "voyage", CefrLevel.B2, ["travel"]),
                Entry("00000000-0000-7000-8000-000000000004", "abstract", CefrLevel.B2, []),
            ]);
            await repository.ImportPackAsync(pack, ct);

            (await repository.ListAsync(LanguagePair.UkrainianToEnglish, [CefrLevel.A1], [], ct)).Select(e => e.Lexeme.Lemma).ShouldBe(["apple", "ticket"]);
            (await repository.ListAsync(LanguagePair.UkrainianToEnglish, [CefrLevel.A1, CefrLevel.B2], [TopicId.From("travel")], ct)).Select(e => e.Lexeme.Lemma).ShouldBe(["ticket", "voyage"]);
            (await repository.ListAsync(LanguagePair.UkrainianToEnglish, [CefrLevel.B2], [TopicId.From("food"), TopicId.From("travel")], ct)).Select(e => e.Lexeme.Lemma).ShouldBe(["voyage"]);
            (await repository.ListAsync(LanguagePair.UkrainianToEnglish, CefrLevelExtensions.All, [], ct)).Count.ShouldBe(4);
            (await repository.ListAsync(LanguagePair.UkrainianToEnglish, [], [], ct)).ShouldBeEmpty();
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // the connection may still hold the file on Windows; the temp folder is cleaned by the OS
            }
        }
    }

    private static WordEntry Entry(string id, string lemma, CefrLevel level, string[] topics) => new(
        new Lexeme(LexemeId.From(Guid.Parse(id)), LanguagePair.UkrainianToEnglish, lemma, PartOfSpeech.Noun, level, topics.Select(TopicId.From).ToArray(), null),
        lemma, [], [lemma], "/x/", "/x/", $"A {lemma}.", "Приклад.");

    private static ImportedContentPack Pack(string packId, WordEntry entry) => new(packId, LanguagePair.UkrainianToEnglish, 1, [entry]);

    private static WordEntry Entry(string[] topics) => new(
        new Lexeme(Receipt, LanguagePair.UkrainianToEnglish, "receipt", PartOfSpeech.Noun, CefrLevel.A2, topics.Select(TopicId.From).ToArray(), null),
        "чек", [], ["receipt"], "/rɪˈsit/", "/rɪˈsiːt/", "Keep the receipt.", "Збережіть чек.");
}
