using System.Text.Json;
using LegacyLens.Core.Model;
using LegacyLens.Core.Retrieval;

namespace LegacyLens.Core.Tests.Retrieval;

public sealed class RetrievalEvaluatorTests
{
    private sealed class ScriptedRetriever(Dictionary<string, string[]> answers) : IRetriever
    {
        public List<int> RequestedTopK { get; } = [];

        public Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, int topK, CancellationToken cancellationToken)
        {
            RequestedTopK.Add(topK);
            IReadOnlyList<KnowledgeChunk> result = answers[query].Select(id => new KnowledgeChunk(id, id, id)).ToList();
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task Computes_per_case_and_mean_metrics()
    {
        var retriever = new ScriptedRetriever(new()
        {
            ["q1"] = ["a", "x", "y"],
            ["q2"] = ["x", "b", "y"],
            ["q3"] = ["x", "y", "z", "c"],
        });
        RetrievalEvalCase[] cases =
        [
            new("c1", "q1", ["a"]),
            new("c2", "q2", ["b"]),
            new("c3", "q3", ["c"]),
        ];

        var result = await RetrievalEvaluator.RunAsync(retriever, cases, 3, CancellationToken.None);

        Assert.Equal(3, result.Cases);
        Assert.Equal(3, result.K);
        Assert.Equal(2.0 / 3, result.MeanRecallAtK, 10);
        Assert.Equal((1.0 + 0.5 + 0.25) / 3, result.MeanReciprocalRank, 10);
        Assert.Equal(["c1", "c2", "c3"], result.PerCase.Select(c => c.Id));
        Assert.Equal([1.0, 1.0, 0.0], result.PerCase.Select(c => c.RecallAtK));
        Assert.Equal([1.0, 0.5, 0.25], result.PerCase.Select(c => c.ReciprocalRank));
        Assert.Equal(["x", "y", "z", "c"], result.PerCase[2].Retrieved);
    }

    [Theory]
    [InlineData(3, 10)]
    [InlineData(1, 10)]
    [InlineData(10, 10)]
    [InlineData(15, 15)]
    public async Task Requests_max_of_k_and_10_from_the_retriever(int k, int expectedTopK)
    {
        var retriever = new ScriptedRetriever(new() { ["q"] = ["a"] });
        await RetrievalEvaluator.RunAsync(retriever, [new("c", "q", ["a"])], k, CancellationToken.None);
        Assert.Equal([expectedTopK], retriever.RequestedTopK);
    }

    [Fact]
    public async Task Recall_uses_only_first_k_but_mrr_uses_whole_list()
    {
        var retriever = new ScriptedRetriever(new() { ["q"] = ["x", "y", "z", "a"] });
        var result = await RetrievalEvaluator.RunAsync(retriever, [new("c", "q", ["a"])], 3, CancellationToken.None);

        Assert.Equal(0.0, result.MeanRecallAtK);
        Assert.Equal(0.25, result.MeanReciprocalRank, 10);
    }

    [Fact]
    public async Task No_cases_gives_zero_means()
    {
        var result = await RetrievalEvaluator.RunAsync(new ScriptedRetriever([]), [], 3, CancellationToken.None);

        Assert.Equal(0, result.Cases);
        Assert.Equal(0, result.MeanRecallAtK);
        Assert.Equal(0, result.MeanReciprocalRank);
        Assert.Empty(result.PerCase);
    }
}

public sealed class RetrievalEvalCasesParseTests
{
    [Fact]
    public void Parses_valid_json_with_camel_case_properties()
    {
        const string json = """
            [
              { "id": "r1", "query": "Session(\"x\")", "expected": ["session-state"] },
              { "id": "r2", "query": "how?", "expected": ["a", "b"] }
            ]
            """;

        var cases = RetrievalEvalCases.Parse(json);

        Assert.Equal(2, cases.Count);
        Assert.Equal("r1", cases[0].Id);
        Assert.Equal("Session(\"x\")", cases[0].Query);
        Assert.Equal(["session-state"], cases[0].Expected);
        Assert.Equal(["a", "b"], cases[1].Expected);
    }

    [Fact]
    public void Empty_array_gives_no_cases()
    {
        Assert.Empty(RetrievalEvalCases.Parse("[]"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[{ \"id\": \"r1\", \"query\": \"q\" }]")]
    [InlineData("[{ \"id\": \"r1\", \"expected\": [\"a\"] }]")]
    [InlineData("[{ \"query\": \"q\", \"expected\": [\"a\"] }]")]
    [InlineData("[{ \"id\": \"r1\", \"query\": \"q\", \"expected\": [] }]")]
    [InlineData("[{ \"id\": \"r1\", \"query\": \"q\", \"expected\": \"a\" }]")]
    [InlineData("[null]")]
    public void Malformed_json_throws_JsonException(string json)
    {
        Assert.ThrowsAny<JsonException>(() => RetrievalEvalCases.Parse(json));
    }

    [Fact]
    public void Null_text_throws()
    {
        Assert.Throws<ArgumentNullException>(() => RetrievalEvalCases.Parse(null!));
    }
}
