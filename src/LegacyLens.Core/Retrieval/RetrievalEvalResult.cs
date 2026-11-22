namespace LegacyLens.Core.Retrieval;

/// <summary>Mean metrics over all eval cases, plus the result of each case.</summary>
public sealed record RetrievalEvalResult(
    int Cases,
    int K,
    double MeanRecallAtK,
    double MeanReciprocalRank,
    IReadOnlyList<RetrievalEvalCaseResult> PerCase);
