using LegacyLens.Core.Model;

namespace LegacyLens.Core.Retrieval;

/// <summary>Finds the knowledge chunks most relevant to a query.</summary>
public interface IRetriever
{
    /// <summary>
    /// Returns at most <paramref name="topK"/> chunks, best first. An empty or whitespace query returns an empty list.
    /// Throws <see cref="ArgumentOutOfRangeException"/> when <paramref name="topK"/> is 0 or less.
    /// </summary>
    Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, int topK, CancellationToken cancellationToken);
}
