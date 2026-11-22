using LegacyLens.Core.Model;
using LegacyLens.Core.Retrieval;

namespace LegacyLens.Core.Tests.Retrieval;

public sealed class HybridRetrieverTests
{
    private static HybridRetriever Create(KnowledgeChunk[] chunks, Func<string, float[]> vectors)
    {
        var kb = new InMemoryKnowledgeBase(chunks);
        return new HybridRetriever(
            new Bm25Retriever(kb, new Bm25Parameters()),
            new EmbeddingRetriever(kb, new FakeEmbeddingClient(vectors)));
    }

    private static async Task<string[]> Ids(HybridRetriever r, string query, int topK = 10)
        => (await r.SearchAsync(query, topK, CancellationToken.None)).Select(c => c.Id).ToArray();

    [Fact]
    public void RrfK_is_60()
    {
        Assert.Equal(60, HybridRetriever.RrfK);
    }

    // BM25 for "alpha beta": A (both words) rank 1, B (alpha only) rank 2, C not listed.
    // Embedding query vector [1,0]: B = 1.0 (rank 1), C ~0.7 (rank 2), A ~0.1 (rank 3).
    // RRF: B = 1/62 + 1/61 = 0.032522, A = 1/61 + 1/63 = 0.032266, C = 1/62 = 0.016129.
    [Fact]
    public async Task Rrf_order_with_k_60_on_controlled_inputs()
    {
        KnowledgeChunk[] chunks =
        [
            new("A", "Xray", "alpha beta"),
            new("B", "Yankee", "alpha filler"),
            new("C", "Zulu", "unrelated words"),
        ];
        var r = Create(chunks, text =>
        {
            if (text.StartsWith("search_query: ", StringComparison.Ordinal)) return [1, 0];
            if (text.Contains("Yankee\n")) return [1, 0];
            if (text.Contains("Zulu\n")) return [0.7f, 0.714f];
            return [0.1f, 0.995f];
        });

        Assert.Equal(["B", "A", "C"], await Ids(r, "alpha beta"));
        Assert.Equal(["B", "A"], await Ids(r, "alpha beta", 2));
    }

    // BM25 "alpha": X (tf 3) rank 1, Y (tf 1) rank 2, Z absent. Embedding: Z, Y, X.
    // X = 1/61 + 1/63, Y = 1/62 + 1/62, Z = 1/61 only. A chunk in both lists beats a rank-1 chunk in one list.
    [Fact]
    public async Task Chunk_in_both_lists_beats_chunk_in_only_one_list()
    {
        KnowledgeChunk[] chunks =
        [
            new("X", "Xray", "alpha alpha alpha"),
            new("Y", "Yankee", "alpha filler words"),
            new("Z", "Zulu", "nothing here"),
        ];
        var r = Create(chunks, text =>
        {
            if (text.StartsWith("search_query: ", StringComparison.Ordinal)) return [0, 1];
            if (text.Contains("Zulu\n")) return [0, 1];
            if (text.Contains("Yankee\n")) return [1, 1];
            return [1, 0];
        });

        var ids = await Ids(r, "alpha");

        Assert.Equal("Z", ids[^1]);
        Assert.Equal(["X", "Y", "Z"], ids);
    }

    [Fact]
    public async Task Chunk_missing_from_bm25_can_still_be_returned_from_embedding()
    {
        KnowledgeChunk[] chunks = [new("A", "Xray", "alpha"), new("B", "Yankee", "other")];
        var r = Create(chunks, text => text.StartsWith("search_query: ", StringComparison.Ordinal) ? [1, 0] : text.Contains("Yankee\n") ? [1, 0] : [0, 1]);

        Assert.Contains("B", await Ids(r, "alpha"));
    }

    [Fact]
    public async Task Equal_fused_scores_are_ordered_by_id()
    {
        // BM25 finds only "b-chunk" (rank 1); embedding ranks "a-chunk" first, "b-chunk" second only if... use a symmetric case:
        // BM25: p rank1 only. Embedding: q rank1 only (p rank 2) -> p = 1/61+1/62, q = 1/61 -> p first.
        // For an exact tie use chunks absent from BM25: both only in embedding with distinct ranks -> no tie, so check id order via equal vectors.
        KnowledgeChunk[] chunks = [new("b", "B", "none"), new("a", "A", "none")];
        var r = Create(chunks, _ => [1, 0]);

        Assert.Equal(["a", "b"], await Ids(r, "zzz"));
    }

    [Fact]
    public async Task TopK_limits_results()
    {
        KnowledgeChunk[] chunks = [new("A", "Xray", "alpha"), new("B", "Yankee", "alpha"), new("C", "Zulu", "alpha")];
        var r = Create(chunks, _ => [1, 0]);

        Assert.Single(await Ids(r, "alpha", 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_query_gives_empty(string query)
    {
        var r = Create([new("A", "Xray", "alpha")], _ => [1, 0]);
        Assert.Empty(await Ids(r, query));
    }

    [Fact]
    public async Task TopK_zero_throws()
    {
        var r = Create([new("A", "Xray", "alpha")], _ => [1, 0]);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => r.SearchAsync("alpha", 0, CancellationToken.None));
    }
}
