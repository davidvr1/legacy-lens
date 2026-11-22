namespace LegacyLens.Core.Migration;

/// <summary>The JSON the language model returns for a migration. Sources are knowledge note ids.</summary>
public sealed record MigrationReply(string Code, IReadOnlyList<string> Notes, IReadOnlyList<string> Sources);
