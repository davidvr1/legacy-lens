using LegacyLens.Core.Model;

namespace LegacyLens.Core.Summary;

/// <summary>The outcome of a summary request: either a summary or a user-facing error, never both.</summary>
public sealed record SummaryResult(CodeSummary? Summary, string? Error);
