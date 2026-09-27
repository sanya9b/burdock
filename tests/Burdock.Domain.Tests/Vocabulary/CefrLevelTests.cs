using Burdock.Domain.Vocabulary;
using Burdock.TestKit;

namespace Burdock.Domain.Tests.Vocabulary;

public sealed class CefrLevelTests
{
    [Fact]
    public void Neighbours_are_clamped_at_the_edges()
    {
        CefrLevel.A1.WithNeighbours().ShouldBe([CefrLevel.A1, CefrLevel.A2]);
        CefrLevel.B1.WithNeighbours().ShouldBe([CefrLevel.A2, CefrLevel.B1, CefrLevel.B2]);
        CefrLevel.C2.WithNeighbours().ShouldBe([CefrLevel.C1, CefrLevel.C2]);
    }

    [Fact]
    public void Free_pool_is_a1_to_b1_and_slugs_are_lower_case()
    {
        CefrLevelExtensions.FreePool.ShouldBe([CefrLevel.A1, CefrLevel.A2, CefrLevel.B1]);
        CefrLevel.B2.ToSlug().ShouldBe("b2");
    }

    [Fact]
    public void Lexeme_builder_produces_a_valid_lexeme_and_rejects_bad_frequency_bands()
    {
        var lexeme = LexemeBuilder.ALexeme().WithLemma("ticket").AtLevel(CefrLevel.A1).InTopics("travel", "shopping").Build();

        lexeme.Lemma.ShouldBe("ticket");
        lexeme.BelongsTo(Domain.SharedKernel.TopicId.From("Travel")).ShouldBeTrue();
        lexeme.FrequencyBand.ShouldBeNull();
        Should.Throw<ArgumentOutOfRangeException>(() => LexemeBuilder.ALexeme().WithFrequencyBand(7).Build());
    }
}
