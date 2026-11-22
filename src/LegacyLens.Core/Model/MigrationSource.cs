namespace LegacyLens.Core.Model;

/// <summary>A knowledge note that the migration proposal cites. Id is the note id; Title is its heading.</summary>
public sealed record MigrationSource(string Id, string Title);
