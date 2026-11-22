using LegacyLens.Core.Model;
using LegacyLens.Core.Retrieval;

namespace LegacyLens.Core.Tests.Retrieval;

public sealed class Bm25RetrieverTests
{
    private static Bm25Retriever Create(IEnumerable<KnowledgeChunk> chunks, Bm25Parameters? p = null)
        => new(new InMemoryKnowledgeBase(chunks), p ?? new Bm25Parameters());

    private static KnowledgeChunk Chunk(string id, string content, string title = "Note") => new(id, title, content);

    private static async Task<string[]> Ids(Bm25Retriever r, string query, int topK = 10)
        => (await r.SearchAsync(query, topK, CancellationToken.None)).Select(c => c.Id).ToArray();

    private static readonly KnowledgeChunk[] Corpus =
    [
        Chunk("recordset", "ADODB Recordset rs.Open rs.EOF MoveNext loop over database rows"),
        Chunk("session", "Session variables Session.Abandon store user state on the server"),
        Chunk("redirect", "Response.Redirect sends the browser to another page"),
    ];

    [Fact]
    public async Task Ranks_matching_chunk_first()
    {
        var r = Create(Corpus);
        Assert.Equal("recordset", (await Ids(r, "ADODB.Recordset rs.EOF"))[0]);
        Assert.Equal("session", (await Ids(r, "Session.Abandon"))[0]);
        Assert.Equal("redirect", (await Ids(r, "Response.Redirect"))[0]);
    }

    [Fact]
    public async Task Search_is_case_insensitive()
    {
        var r = Create(Corpus);
        Assert.Equal(await Ids(r, "adodb recordset"), await Ids(r, "ADODB RECORDSET"));
    }

    [Fact]
    public async Task Camel_case_parts_match()
    {
        var r = Create([Chunk("a", "Use QueryString values"), Chunk("b", "nothing relevant here")]);
        Assert.Equal(["a"], await Ids(r, "query string"));
    }

    [Fact]
    public async Task Title_is_indexed()
    {
        var r = Create([Chunk("a", "body text", title: "Zebra Crossing"), Chunk("b", "body text")]);
        Assert.Equal(["a"], await Ids(r, "zebra"));
    }

    [Fact]
    public async Task TopK_limits_results()
    {
        var chunks = Enumerable.Range(0, 6).Select(i => Chunk($"c{i}", "shared word"));
        var r = Create(chunks);

        Assert.Equal(2, (await Ids(r, "shared", 2)).Length);
        Assert.Equal(6, (await Ids(r, "shared", 100)).Length);
    }

    [Fact]
    public async Task Only_matching_chunks_are_returned()
    {
        var r = Create(Corpus);
        Assert.Equal(["session"], await Ids(r, "Abandon"));
    }

    [Fact]
    public async Task No_match_gives_empty()
    {
        var r = Create(Corpus);
        Assert.Empty(await Ids(r, "kubernetes"));
    }

    [Fact]
    public async Task Stopword_only_query_gives_empty()
    {
        var r = Create(Corpus);
        Assert.Empty(await Ids(r, "the of and"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task Empty_or_whitespace_query_gives_empty(string query)
    {
        var r = Create(Corpus);
        Assert.Empty(await Ids(r, query));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TopK_zero_or_negative_throws(int topK)
    {
        var r = Create(Corpus);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => r.SearchAsync("session", topK, CancellationToken.None));
    }

    [Fact]
    public async Task TopK_is_validated_even_for_empty_query()
    {
        var r = Create(Corpus);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => r.SearchAsync("", 0, CancellationToken.None));
    }

    [Fact]
    public async Task Empty_corpus_gives_empty()
    {
        var r = Create([]);
        Assert.Empty(await Ids(r, "anything"));
    }

    [Fact]
    public async Task Ties_are_ordered_by_id_ordinal()
    {
        var r = Create([Chunk("b", "same text"), Chunk("C", "same text"), Chunk("a", "same text")]);
        // ordinal: uppercase 'C' sorts before lowercase letters
        Assert.Equal(["C", "a", "b"], await Ids(r, "same"));
    }

    [Fact]
    public async Task Repeating_a_query_term_does_not_change_ranking()
    {
        var r = Create(Corpus);
        Assert.Equal(await Ids(r, "session"), await Ids(r, "session session session"));
    }

    [Fact]
    public async Task Duplicate_query_terms_count_once()
    {
        // N=4. idf(alpha, df=3) = ln(1+1.5/3.5) = 0.357; idf(beta, df=1) = ln(1+3.5/1.5) = 1.204.
        // Counted once: y (beta) wins. If the 4 repeated "alpha" counted 4 times (1.43 > 1.204), x would win.
        var r = Create(
        [
            Chunk("x", "alpha filler"),
            Chunk("y", "beta filler"),
            Chunk("p", "alpha filler"),
            Chunk("q", "alpha filler"),
        ]);
        var ranked = await Ids(r, "alpha alpha alpha alpha beta");
        Assert.Equal("y", ranked[0]);
    }

    [Fact]
    public async Task Rarer_term_outweighs_common_term()
    {
        var r = Create(
        [
            Chunk("common-only", "common filler"),
            Chunk("rare-only", "rare filler"),
            Chunk("c2", "common filler"),
            Chunk("c3", "common filler"),
        ]);
        Assert.Equal("rare-only", (await Ids(r, "common rare"))[0]);
    }

    // Hand-computed: A = "ta" + "apple apple" + 7 words = 10 tokens (tf=2); B = "tb apple" = 2 tokens (tf=1);
    // C = 6 tokens, no match. avgLen = 6, N=3, df=2 (same idf for A and B, so it cancels in the comparison).
    // k1=1.2. b=0: A = 2*2.2/(2+1.2)=1.375*idf, B = 2.2/(1+1.2)=1.0*idf  => A first.
    // b=1: A = 4.4/(2+1.2*10/6)=1.1*idf, B = 2.2/(1+1.2*2/6)=1.571*idf => B first.
    private static KnowledgeChunk[] LengthCorpus() =>
    [
        new("A", "ta", "apple apple w1 w2 w3 w4 w5 w6 w7"),
        new("B", "tb", "apple"),
        new("C", "tc", "c1 c2 c3 c4 c5"),
    ];

    [Fact]
    public async Task B_zero_removes_length_normalization()
    {
        var r = Create(LengthCorpus(), new Bm25Parameters(K1: 1.2, B: 0));
        Assert.Equal(["A", "B"], await Ids(r, "apple"));
    }

    [Fact]
    public async Task B_one_fully_normalizes_for_length()
    {
        var r = Create(LengthCorpus(), new Bm25Parameters(K1: 1.2, B: 1));
        Assert.Equal(["B", "A"], await Ids(r, "apple"));
    }

    [Fact]
    public async Task Default_parameters_match_hand_computation()
    {
        // b=0.75: A = 4.4/(2+1.2*(0.25+0.75*10/6)) = 4.4/3.8 = 1.158 ; B = 2.2/(1+1.2*(0.25+0.75*2/6)) = 2.2/1.6 = 1.375 => B first
        var r = Create(LengthCorpus());
        Assert.Equal(["B", "A"], await Ids(r, "apple"));
    }

    [Fact]
    public async Task K1_zero_ignores_term_frequency()
    {
        var chunks = new[]
        {
            Chunk("z-many", "apple apple apple", title: "tt"),
            Chunk("a-one", "apple xx yy", title: "tt"),
        };
        // k1=0, b=0: score = idf for any tf>0, so a tie broken by id. With k1=1.2, b=0 more occurrences win.
        var flat = Create(chunks, new Bm25Parameters(K1: 0, B: 0));
        Assert.Equal(["a-one", "z-many"], await Ids(flat, "apple"));

        var normal = Create(chunks, new Bm25Parameters(K1: 1.2, B: 0));
        Assert.Equal(["z-many", "a-one"], await Ids(normal, "apple"));
    }

    [Fact]
    public async Task Knowledge_base_is_read_once_across_searches()
    {
        var kb = new CountingKnowledgeBase(Corpus);
        var r = new Bm25Retriever(kb, new Bm25Parameters());

        await r.SearchAsync("session", 3, CancellationToken.None);
        await r.SearchAsync("redirect", 3, CancellationToken.None);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => r.SearchAsync("recordset", 3, CancellationToken.None)));

        Assert.Equal(1, kb.Calls);
    }

    [Fact]
    public async Task Null_query_throws()
    {
        var r = Create(Corpus);
        await Assert.ThrowsAsync<ArgumentNullException>(() => r.SearchAsync(null!, 3, CancellationToken.None));
    }

    private sealed class CountingKnowledgeBase(IEnumerable<KnowledgeChunk> chunks) : IKnowledgeBase
    {
        private readonly IReadOnlyList<KnowledgeChunk> _chunks = chunks.ToList();
        private int _calls;

        public int Calls => _calls;

        public Task<IReadOnlyList<KnowledgeChunk>> GetChunksAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(_chunks);
        }
    }
}
