using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Retrieval;

/// <summary>
/// Ranks chunks by cosine similarity of embedding vectors. Chunk vectors are computed once, on the first search,
/// with one embedding call. If that fails, the next search tries again.
/// </summary>
public sealed class EmbeddingRetriever(IKnowledgeBase knowledgeBase, IEmbeddingClient embeddingClient) : IRetriever
{
    public const int MaxQueryLength = 4000;

    private const string DocumentPrefix = "search_document: ";
    private const string QueryPrefix = "search_query: ";

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile Vectors? _vectors;

    public async Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, int topK, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var vectors = await GetVectorsAsync(cancellationToken);

        var truncated = query.Length > MaxQueryLength ? query[..MaxQueryLength] : query;
        var queryEmbeddings = await embeddingClient.EmbedAsync([QueryPrefix + truncated], cancellationToken);
        var queryVector = queryEmbeddings[0];

        var scored = new List<(KnowledgeChunk Chunk, double Similarity)>();
        for (var i = 0; i < vectors.Chunks.Count; i++)
        {
            scored.Add((vectors.Chunks[i], Cosine(queryVector, vectors.Embeddings[i])));
        }

        return scored
            .OrderByDescending(s => s.Similarity)
            .ThenBy(s => s.Chunk.Id, StringComparer.Ordinal)
            .Take(topK)
            .Select(s => s.Chunk)
            .ToList();
    }

    private static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new LlmInvalidResponseException(
                $"Embedding vectors have different lengths ({a.Length} and {b.Length}). Was the embedding model changed?");
        }

        double dot = 0;
        double normA = 0;
        double normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            normA += (double)a[i] * a[i];
            normB += (double)b[i] * b[i];
        }

        return normA == 0 || normB == 0 ? 0 : dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private async Task<Vectors> GetVectorsAsync(CancellationToken cancellationToken)
    {
        var existing = _vectors;
        if (existing is not null)
        {
            return existing;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            _vectors ??= await BuildVectorsAsync(cancellationToken);
            return _vectors;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<Vectors> BuildVectorsAsync(CancellationToken cancellationToken)
    {
        var chunks = await knowledgeBase.GetChunksAsync(cancellationToken);
        var texts = chunks.Select(c => DocumentPrefix + c.Title + "\n" + c.Content).ToList();

        IReadOnlyList<float[]> embeddings = texts.Count == 0
            ? Array.Empty<float[]>()
            : await embeddingClient.EmbedAsync(texts, cancellationToken);

        return new Vectors(chunks, embeddings);
    }

    private sealed record Vectors(IReadOnlyList<KnowledgeChunk> Chunks, IReadOnlyList<float[]> Embeddings);
}
