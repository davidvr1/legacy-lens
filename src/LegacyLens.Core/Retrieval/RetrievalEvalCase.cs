namespace LegacyLens.Core.Retrieval;

/// <summary>One retrieval eval case: a query and the knowledge chunk ids that should be found.</summary>
public sealed record RetrievalEvalCase(string Id, string Query, IReadOnlyList<string> Expected);
