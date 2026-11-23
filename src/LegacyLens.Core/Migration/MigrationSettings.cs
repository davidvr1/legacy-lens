using LegacyLens.Core.Llm;

namespace LegacyLens.Core.Migration;

/// <summary>Settings for the migration service. Registered in DI from the Retrieval and Llm sections (Core has no options package).</summary>
public sealed record MigrationSettings(int TopK, PromptVariant Variant = PromptVariant.ZeroShot);
