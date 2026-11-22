namespace LegacyLens.Core.Retrieval;

/// <summary>The result of one retrieval eval case. Retrieved holds the chunk ids in rank order.</summary>
public sealed record RetrievalEvalCaseResult(string Id, double RecallAtK, double ReciprocalRank, IReadOnlyList<string> Retrieved);
