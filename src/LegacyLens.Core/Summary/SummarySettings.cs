using LegacyLens.Core.Llm;

namespace LegacyLens.Core.Summary;

/// <summary>Settings for the summary service. Registered in DI from the Llm section.</summary>
public sealed record SummarySettings(PromptVariant Variant);
