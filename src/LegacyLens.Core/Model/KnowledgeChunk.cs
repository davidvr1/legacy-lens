namespace LegacyLens.Core.Model;

/// <summary>One knowledge note used for retrieval. Id is the file name without ".md"; Content is the whole Markdown text.</summary>
public sealed record KnowledgeChunk(string Id, string Title, string Content);
