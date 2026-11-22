namespace LegacyLens.Core.Retrieval;

/// <summary>Runs a retriever over eval cases and averages recall@k and MRR.</summary>
public static class RetrievalEvaluator
{
    /// <summary>Searches for max(k, 10) chunks per case, so the per-case results show more than k when available.</summary>
    public static async Task<RetrievalEvalResult> RunAsync(
        IRetriever retriever,
        IReadOnlyList<RetrievalEvalCase> cases,
        int k,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(retriever);
        ArgumentNullException.ThrowIfNull(cases);

        var results = new List<RetrievalEvalCaseResult>();
        foreach (var evalCase in cases)
        {
            var chunks = await retriever.SearchAsync(evalCase.Query, Math.Max(k, 10), cancellationToken);
            var retrieved = chunks.Select(c => c.Id).ToList();

            results.Add(new RetrievalEvalCaseResult(
                evalCase.Id,
                RetrievalMetrics.RecallAtK(evalCase.Expected, retrieved, k),
                RetrievalMetrics.ReciprocalRank(evalCase.Expected, retrieved),
                retrieved));
        }

        var meanRecall = results.Count == 0 ? 0 : results.Average(r => r.RecallAtK);
        var meanRank = results.Count == 0 ? 0 : results.Average(r => r.ReciprocalRank);
        return new RetrievalEvalResult(results.Count, k, meanRecall, meanRank, results);
    }
}
