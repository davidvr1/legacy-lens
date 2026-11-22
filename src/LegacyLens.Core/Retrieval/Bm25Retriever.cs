using LegacyLens.Core.Model;

namespace LegacyLens.Core.Retrieval;

/// <summary>Ranks chunks with Okapi BM25 over Title and Content. The index is built once, on the first search.</summary>
public sealed class Bm25Retriever(IKnowledgeBase knowledgeBase, Bm25Parameters parameters) : IRetriever
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile Index? _index;

    public async Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, int topK, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var index = await GetIndexAsync(cancellationToken);
        var terms = Tokenizer.Tokenize(query).Distinct(StringComparer.Ordinal).ToList();

        var scored = new List<(KnowledgeChunk Chunk, double Score)>();
        for (var i = 0; i < index.Chunks.Count; i++)
        {
            var score = ScoreChunk(index, i, terms);
            if (score > 0)
            {
                scored.Add((index.Chunks[i], score));
            }
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Chunk.Id, StringComparer.Ordinal)
            .Take(topK)
            .Select(s => s.Chunk)
            .ToList();
    }

    private double ScoreChunk(Index index, int chunkIndex, IReadOnlyList<string> terms)
    {
        var k1 = parameters.K1;
        var b = parameters.B;
        var length = index.Lengths[chunkIndex];
        var score = 0.0;

        foreach (var term in terms)
        {
            if (!index.TermFrequencies[chunkIndex].TryGetValue(term, out var tf))
            {
                continue;
            }

            var df = index.DocumentFrequencies[term];
            var idf = Math.Log(1 + (index.Chunks.Count - df + 0.5) / (df + 0.5));
            var norm = tf + k1 * (1 - b + b * length / index.AverageLength);
            score += idf * tf * (k1 + 1) / norm;
        }

        return score;
    }

    private async Task<Index> GetIndexAsync(CancellationToken cancellationToken)
    {
        var existing = _index;
        if (existing is not null)
        {
            return existing;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            _index ??= await BuildIndexAsync(cancellationToken);
            return _index;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<Index> BuildIndexAsync(CancellationToken cancellationToken)
    {
        var chunks = await knowledgeBase.GetChunksAsync(cancellationToken);
        var termFrequencies = new List<Dictionary<string, int>>();
        var lengths = new List<int>();
        var documentFrequencies = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var chunk in chunks)
        {
            var tokens = Tokenizer.Tokenize(chunk.Title + "\n" + chunk.Content);
            var tf = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var token in tokens)
            {
                tf[token] = tf.GetValueOrDefault(token) + 1;
            }

            foreach (var token in tf.Keys)
            {
                documentFrequencies[token] = documentFrequencies.GetValueOrDefault(token) + 1;
            }

            termFrequencies.Add(tf);
            lengths.Add(tokens.Count);
        }

        var averageLength = lengths.Count == 0 ? 0 : lengths.Average();
        return new Index(chunks, termFrequencies, lengths, averageLength, documentFrequencies);
    }

    private sealed record Index(
        IReadOnlyList<KnowledgeChunk> Chunks,
        IReadOnlyList<Dictionary<string, int>> TermFrequencies,
        IReadOnlyList<int> Lengths,
        double AverageLength,
        Dictionary<string, int> DocumentFrequencies);
}
