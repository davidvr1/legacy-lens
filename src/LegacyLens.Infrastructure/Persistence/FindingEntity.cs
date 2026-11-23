using LegacyLens.Core.Model;

namespace LegacyLens.Infrastructure.Persistence;

/// <summary>One rule finding of a saved analysis (table <c>Findings</c>).</summary>
public sealed class FindingEntity
{
    /// <summary>Identity column. Its order is the order the findings were produced in.</summary>
    public int Id { get; set; }

    public Guid AnalysisId { get; set; }

    public string RuleId { get; set; } = string.Empty;

    public Severity Severity { get; set; }

    public int Line { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Explanation { get; set; } = string.Empty;

    public string Fix { get; set; } = string.Empty;

    /// <summary>Where the finding came from. Always "rule" for now.</summary>
    public string Source { get; set; } = "rule";
}
