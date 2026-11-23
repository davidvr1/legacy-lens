using LegacyLens.Core.Llm;

namespace LegacyLens.Core.Evaluation;

/// <summary>Which tasks to run for a case, and the prompt variant and retrieval depth to use.</summary>
public sealed record LlmEvalOptions(bool RunSummary, bool RunMigration, PromptVariant Variant, int TopK);
