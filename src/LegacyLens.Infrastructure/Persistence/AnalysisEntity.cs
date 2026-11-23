using LegacyLens.Core.Model;

namespace LegacyLens.Infrastructure.Persistence;

/// <summary>One saved analysis (table <c>Analyses</c>). Its findings are <see cref="FindingEntity"/> rows.</summary>
public sealed class AnalysisEntity
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public SourceLanguage Language { get; set; }

    public string Code { get; set; } = string.Empty;

    /// <summary>The serialized summary result (summary and error), or null when there is neither.</summary>
    public string? SummaryJson { get; set; }

    /// <summary>The serialized migration result (proposal and error), or null when there is neither.</summary>
    public string? MigrationJson { get; set; }

    /// <summary>Whether the migration compiles. Null when there is no migration.</summary>
    public bool? CompilesOk { get; set; }

    /// <summary>UTC. A DateTime (not DateTimeOffset) so the history can be ordered on both SQL Server and SQLite.</summary>
    public DateTime CreatedAt { get; set; }

    public List<FindingEntity> Findings { get; set; } = [];
}
