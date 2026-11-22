namespace LegacyLens.Core.Migration;

/// <summary>Settings for the migration service. Registered in DI from the Retrieval section (Core has no options package).</summary>
public sealed record MigrationSettings(int TopK);
