using LegacyLens.Core.Model;

namespace LegacyLens.Core.Evaluation;

/// <summary>One hand-written evaluation case: a legacy file and the facts a correct summary should state about it.</summary>
public sealed record LlmEvalCase(string Id, SourceFile File, IReadOnlyList<ExpectedFact> Facts);
