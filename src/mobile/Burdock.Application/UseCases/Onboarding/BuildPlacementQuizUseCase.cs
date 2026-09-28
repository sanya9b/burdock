using Burdock.Application.Ports;
using Burdock.Domain.Learning;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.UseCases.Onboarding;

/// <summary>
/// Builds the S-03a placement mini-test from the local content: up to <see cref="LevelPlacement.QuestionCount"/> words spread
/// over the levels the pool offers, easy first (spec 03). The seed comes from the random port so a run is reproducible in tests.
/// </summary>
public sealed class BuildPlacementQuizUseCase(ILearnerContext learner, IContentRepository content, IRandom random)
{
    public async Task<IReadOnlyList<WordEntry>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var pool = await content.ListAsync(settings.Pair, CefrLevelExtensions.All, [], cancellationToken).ConfigureAwait(false);
        return LevelPlacement.Pick(pool, entry => entry.Lexeme.Level, random.Next(0, int.MaxValue));
    }
}
