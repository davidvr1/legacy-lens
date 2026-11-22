using LegacyLens.Core.Model;

namespace LegacyLens.Core.Retrieval;

/// <summary>Merges the BM25 and embedding rankings with Reciprocal Rank Fusion.</summary>
public sealed class HybridRetriever(Bm25Retriever bm25, EmbeddingRetriever embedding) : IRetriever
{
    public const int RrfK = 60;

    public async Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, int topK, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var candidateCount = Math.Max(topK * 3, 10);
        var bm25Results = await bm25.SearchAsync(query, candidateCount, cancellationToken);
        var embeddingResults = await embedding.SearchAsync(query, candidateCount, cancellationToken);

        var fused = new Dictionary<string, (KnowledgeChunk Chunk, double Score)>(StringComparer.Ordinal);
        AddReciprocalRanks(fused, bm25Results);
        AddReciprocalRanks(fused, embeddingResults);

        return fused.Values
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Chunk.Id, StringComparer.Ordinal)
            .Take(topK)
            .Select(s => s.Chunk)
            .ToList();
    }

    private static void AddReciprocalRanks(
        Dictionary<string, (KnowledgeChunk Chunk, double Score)> fused,
        IReadOnlyList<KnowledgeChunk> ranked)
    {
        for (var i = 0; i < ranked.Count; i++)
        {
            var chunk = ranked[i];
            var contribution = 1.0 / (RrfK + i + 1);
            if (fused.TryGetValue(chunk.Id, out var existing))
            {
                fused[chunk.Id] = (existing.Chunk, existing.Score + contribution);
            }
            else
            {
                fused[chunk.Id] = (chunk, contribution);
            }
        }
    }
}
