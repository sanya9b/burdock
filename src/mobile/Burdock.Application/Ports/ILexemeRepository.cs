using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.Ports;

/// <summary>Read access to lexemes from the locally cached content packs (spec 08 §3.10, spec 01 §3.4).</summary>
public interface ILexemeRepository
{
    Task<Lexeme?> FindAsync(LexemeId id, CancellationToken cancellationToken);

    /// <summary>Lexemes of a pair filtered by level and, optionally, topics; empty topics means all topics.</summary>
    Task<IReadOnlyList<Lexeme>> ListAsync(LanguagePair pair, IReadOnlyCollection<CefrLevel> levels, IReadOnlyCollection<TopicId> topics, CancellationToken cancellationToken);
}
