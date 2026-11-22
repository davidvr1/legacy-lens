using LegacyLens.Core.Model;

namespace LegacyLens.Core.Retrieval;

/// <summary>Builds a <see cref="KnowledgeChunk"/> from a Markdown note.</summary>
public static class KnowledgeParser
{
    private const string HeadingPrefix = "# ";

    /// <summary>The title is the text of the first line that starts with "# ". Without one, the id is used.</summary>
    public static KnowledgeChunk Parse(string id, string markdown)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(markdown);

        var heading = markdown
            .Split('\n')
            .FirstOrDefault(line => line.StartsWith(HeadingPrefix, StringComparison.Ordinal));

        var title = heading is null ? string.Empty : heading[HeadingPrefix.Length..].Trim();
        return new KnowledgeChunk(id, string.IsNullOrWhiteSpace(title) ? id : title, markdown);
    }
}
