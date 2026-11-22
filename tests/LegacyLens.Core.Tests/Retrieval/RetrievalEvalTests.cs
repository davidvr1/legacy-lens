using LegacyLens.Core.Retrieval;
using Xunit.Abstractions;

namespace LegacyLens.Core.Tests.Retrieval;

/// <summary>Runs BM25 (default parameters) over the real knowledge notes and evals/retrieval.json.</summary>
public sealed class RetrievalEvalTests(ITestOutputHelper output)
{
    private static string KnowledgeFolder => Path.Combine(AppContext.BaseDirectory, "knowledge");

    private static IReadOnlyList<RetrievalEvalCase> LoadCases()
        => RetrievalEvalCases.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "evals", "retrieval.json")));

    private static InMemoryKnowledgeBase LoadKnowledge()
        => new(Directory.GetFiles(KnowledgeFolder, "*.md")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => KnowledgeParser.Parse(Path.GetFileNameWithoutExtension(f), File.ReadAllText(f))));

    [Fact]
    public async Task Knowledge_folder_has_12_notes_with_titles()
    {
        var chunks = await LoadKnowledge().GetChunksAsync(CancellationToken.None);
        Assert.Equal(12, chunks.Count);
        Assert.All(chunks, c => Assert.NotEqual(c.Id, c.Title));
    }

    [Fact]
    public void Eval_file_has_22_cases_with_one_or_two_expected_ids_each()
    {
        var cases = LoadCases();
        Assert.Equal(22, cases.Count);
        Assert.All(cases, c => Assert.InRange(c.Expected.Count, 1, 2));
        Assert.Equal(cases.Count, cases.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Every_expected_id_exists_as_a_knowledge_file()
    {
        var ids = Directory.GetFiles(KnowledgeFolder, "*.md").Select(Path.GetFileNameWithoutExtension).ToHashSet();
        foreach (var evalCase in LoadCases())
        {
            foreach (var expected in evalCase.Expected)
            {
                Assert.True(ids.Contains(expected), $"Case {evalCase.Id}: expected id '{expected}' has no knowledge file.");
            }
        }
    }

    [Fact]
    public async Task Bm25_recall_at_3_is_at_least_0_9()
    {
        var retriever = new Bm25Retriever(LoadKnowledge(), new Bm25Parameters());
        var result = await RetrievalEvaluator.RunAsync(retriever, LoadCases(), 3, CancellationToken.None);

        foreach (var c in result.PerCase)
        {
            output.WriteLine($"{c.Id}: recall@3={c.RecallAtK:0.000} rr={c.ReciprocalRank:0.000} top3=[{string.Join(", ", c.Retrieved.Take(3))}]");
        }

        output.WriteLine($"Cases={result.Cases} K={result.K} MeanRecall@3={result.MeanRecallAtK:0.000} MRR={result.MeanReciprocalRank:0.000}");

        Assert.True(result.MeanRecallAtK >= 0.9, $"Mean recall@3 was {result.MeanRecallAtK:0.000}");
    }
}
