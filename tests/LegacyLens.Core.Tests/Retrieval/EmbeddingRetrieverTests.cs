using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;
using LegacyLens.Core.Retrieval;

namespace LegacyLens.Core.Tests.Retrieval;

public sealed class EmbeddingRetrieverTests
{
    private static readonly KnowledgeChunk[] Chunks =
    [
        new("north", "North", "about north"),
        new("east", "East", "about east"),
        new("diag", "Diagonal", "about diag"),
    ];

    // Document vectors by title; the query vector by keyword after the query prefix.
    private static float[] Vector(string text)
    {
        if (text.StartsWith("search_query: ", StringComparison.Ordinal))
        {
            if (text.Contains("go-north")) return [1, 0];
            if (text.Contains("go-east")) return [0, 1];
            if (text.Contains("go-diag")) return [1, 1];
            return [0.1f, 0.1f];
        }

        if (text.Contains("North\n")) return [1, 0];
        if (text.Contains("East\n")) return [0, 1];
        return [1, 1];
    }

    private static (EmbeddingRetriever Retriever, FakeEmbeddingClient Client) Create(IEnumerable<KnowledgeChunk>? chunks = null, Func<string, float[]>? vectors = null)
    {
        var client = new FakeEmbeddingClient(vectors ?? Vector);
        return (new EmbeddingRetriever(new InMemoryKnowledgeBase(chunks ?? Chunks), client), client);
    }

    private static async Task<string[]> Ids(EmbeddingRetriever r, string query, int topK = 10)
        => (await r.SearchAsync(query, topK, CancellationToken.None)).Select(c => c.Id).ToArray();

    [Fact]
    public async Task Ranks_by_cosine_similarity()
    {
        var (r, _) = Create();
        Assert.Equal(["north", "diag", "east"], await Ids(r, "go-north"));
        Assert.Equal(["east", "diag", "north"], await Ids(r, "go-east"));
        Assert.Equal(["diag", "east", "north"], await Ids(r, "go-diag"));
    }

    [Fact]
    public async Task Cosine_ignores_vector_magnitude()
    {
        var (r, _) = Create(vectors: text =>
        {
            if (text.StartsWith("search_query: ", StringComparison.Ordinal)) return [1000, 0];
            if (text.Contains("North\n")) return [0.001f, 0];
            if (text.Contains("East\n")) return [1, 5];
            return [1, 1];
        });
        Assert.Equal("north", (await Ids(r, "q"))[0]);
    }

    [Fact]
    public async Task TopK_limits_results()
    {
        var (r, _) = Create();
        Assert.Equal(["north"], await Ids(r, "go-north", 1));
        Assert.Equal(3, (await Ids(r, "go-north", 50)).Length);
    }

    [Fact]
    public async Task Equal_similarity_is_ordered_by_id()
    {
        var chunks = new[] { new KnowledgeChunk("b", "B", "x"), new KnowledgeChunk("a", "A", "x") };
        var (r, _) = Create(chunks, _ => [1, 0]);
        Assert.Equal(["a", "b"], await Ids(r, "anything"));
    }

    [Fact]
    public async Task Zero_vectors_do_not_produce_NaN_or_throw()
    {
        var (r, _) = Create(vectors: _ => [0, 0]);
        Assert.Equal(3, (await Ids(r, "q")).Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \t")]
    public async Task Empty_or_whitespace_query_gives_empty_without_embedding_calls(string query)
    {
        var (r, client) = Create();
        Assert.Empty(await Ids(r, query));
        Assert.Empty(client.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task TopK_zero_or_negative_throws(int topK)
    {
        var (r, _) = Create();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => r.SearchAsync("q", topK, CancellationToken.None));
    }

    [Fact]
    public async Task Document_and_query_prefixes_are_sent()
    {
        var (r, client) = Create();
        await Ids(r, "go-north");

        var documentCall = client.Calls[0];
        Assert.Equal(Chunks.Length, documentCall.Count);
        Assert.Equal("search_document: North\nabout north", documentCall[0]);
        Assert.All(documentCall, t => Assert.StartsWith("search_document: ", t));

        var queryCall = client.Calls[1];
        Assert.Equal(["search_query: go-north"], queryCall);
    }

    [Fact]
    public async Task Query_is_truncated_to_MaxQueryLength_before_the_prefix()
    {
        Assert.Equal(4000, EmbeddingRetriever.MaxQueryLength);
        var (r, client) = Create();
        var longQuery = new string('x', EmbeddingRetriever.MaxQueryLength + 500);

        await Ids(r, longQuery);

        var sent = client.Calls[1].Single();
        Assert.Equal("search_query: " + new string('x', EmbeddingRetriever.MaxQueryLength), sent);
    }

    [Fact]
    public async Task Query_of_exactly_MaxQueryLength_is_not_truncated()
    {
        var (r, client) = Create();
        var query = new string('y', EmbeddingRetriever.MaxQueryLength);

        await Ids(r, query);

        Assert.Equal("search_query: " + query, client.Calls[1].Single());
    }

    [Fact]
    public async Task Chunk_vectors_are_embedded_once_in_one_batch_across_searches()
    {
        var (r, client) = Create();

        await Ids(r, "go-north");
        await Ids(r, "go-east");
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Ids(r, "go-diag")));

        var batchCalls = client.Calls.Where(c => c.Count > 1).ToList();
        Assert.Single(batchCalls);
        Assert.Equal(Chunks.Length, batchCalls[0].Count);
        Assert.Equal(1 + 8, client.Calls.Count); // one batch + one call per search (8 searches)
    }

    [Fact]
    public async Task Failed_init_is_retried_by_the_next_search()
    {
        var (r, client) = Create();
        client.FailuresRemaining = 1;

        await Assert.ThrowsAsync<LlmException>(() => Ids(r, "go-north"));
        Assert.Equal(["north", "diag", "east"], await Ids(r, "go-north"));

        // 1 failed batch, 1 successful batch, 1 query.
        Assert.Equal(3, client.Calls.Count);
        Assert.Equal(Chunks.Length, client.Calls[1].Count);
    }

    [Fact]
    public async Task Query_vector_length_mismatch_throws_LlmInvalidResponseException()
    {
        var (r, _) = Create(vectors: text => text.StartsWith("search_query: ", StringComparison.Ordinal) ? [1, 0, 0] : [1, 0]);
        await Assert.ThrowsAsync<LlmInvalidResponseException>(() => Ids(r, "q"));
    }

    [Fact]
    public async Task Empty_knowledge_base_gives_empty_and_no_embedding_of_documents()
    {
        var (r, client) = Create(chunks: []);
        Assert.Empty(await Ids(r, "q"));
        Assert.DoesNotContain(client.Calls, c => c.Any(t => t.StartsWith("search_document", StringComparison.Ordinal)));
    }
}
