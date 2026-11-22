using LegacyLens.Core.Model;

namespace LegacyLens.Core.Retrieval;

/// <summary>A knowledge base with fixed chunks. Used by tests and the eval app.</summary>
public sealed class InMemoryKnowledgeBase(IEnumerable<KnowledgeChunk> chunks) : IKnowledgeBase
{
    private readonly IReadOnlyList<KnowledgeChunk> _chunks = chunks.ToList();

    public Task<IReadOnlyList<KnowledgeChunk>> GetChunksAsync(CancellationToken cancellationToken)
        => Task.FromResult(_chunks);
}
