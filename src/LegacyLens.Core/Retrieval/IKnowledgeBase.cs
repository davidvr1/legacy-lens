using LegacyLens.Core.Model;

namespace LegacyLens.Core.Retrieval;

/// <summary>Provides the knowledge chunks that retrievers search.</summary>
public interface IKnowledgeBase
{
    Task<IReadOnlyList<KnowledgeChunk>> GetChunksAsync(CancellationToken cancellationToken);
}
