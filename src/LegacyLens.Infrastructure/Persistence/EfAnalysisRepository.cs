using LegacyLens.Core.Analysis;
using LegacyLens.Core.Migration;
using LegacyLens.Core.Model;
using LegacyLens.Core.Summary;
using Microsoft.EntityFrameworkCore;

namespace LegacyLens.Infrastructure.Persistence;

/// <summary>Saves and reads analyses with EF Core (SQL Server in the app, SQLite in tests).</summary>
public sealed class EfAnalysisRepository(LegacyLensDbContext db, TimeProvider timeProvider) : IAnalysisRepository
{
    private const string RuleSource = "rule";

    public async Task<StoredAnalysis> SaveAsync(SourceFile file, AnalysisResult result, CancellationToken cancellationToken)
    {
        var stored = new StoredAnalysis(Guid.NewGuid(), file.FileName, file.Language, file.Code, timeProvider.GetUtcNow(), result);

        db.Analyses.Add(new AnalysisEntity
        {
            Id = stored.Id,
            FileName = file.FileName,
            Language = file.Language,
            Code = file.Code,
            SummaryJson = result.Summary is null && result.SummaryError is null
                ? null
                : AnalysisJson.Serialize(new SummaryResult(result.Summary, result.SummaryError)),
            MigrationJson = result.Migration is null && result.MigrationError is null
                ? null
                : AnalysisJson.Serialize(new MigrationResult(result.Migration, result.MigrationError)),
            CompilesOk = result.Migration?.CompilesOk,
            CreatedAt = stored.CreatedAt.UtcDateTime,
            Findings = result.Findings.Select(f => new FindingEntity
            {
                RuleId = f.RuleId,
                Severity = f.Severity,
                Line = f.Line,
                Title = f.Title,
                Explanation = f.Explanation,
                Fix = f.Fix,
                Source = RuleSource,
            }).ToList(),
        });

        await db.SaveChangesAsync(cancellationToken);
        return stored;
    }

    public async Task<IReadOnlyList<AnalysisListItem>> ListAsync(int limit, CancellationToken cancellationToken)
    {
        var rows = await db.Analyses.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new { a.Id, a.FileName, a.Language, a.CreatedAt })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(r => r.Id).ToList();
        var counts = await db.Findings.AsNoTracking()
            .Where(f => ids.Contains(f.AnalysisId))
            .GroupBy(f => new { f.AnalysisId, f.Severity })
            .Select(g => new { g.Key.AnalysisId, g.Key.Severity, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AnalysisListItem(
            r.Id,
            r.FileName,
            r.Language,
            AsUtc(r.CreatedAt),
            ToCounts(counts.Where(c => c.AnalysisId == r.Id).Select(c => (c.Severity, c.Count))))).ToList();
    }

    public async Task<StoredAnalysis?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Analyses.AsNoTracking()
            .Include(a => a.Findings)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var summary = entity.SummaryJson is null
            ? new SummaryResult(null, null)
            : AnalysisJson.Read<SummaryResult>(entity.SummaryJson);
        var migration = entity.MigrationJson is null
            ? new MigrationResult(null, null)
            : AnalysisJson.Read<MigrationResult>(entity.MigrationJson);

        var findings = entity.Findings
            .OrderBy(f => f.Id)
            .Select(f => new Finding(f.RuleId, f.Severity, f.Line, f.Title, f.Explanation, f.Fix))
            .ToList();

        var result = new AnalysisResult(findings, summary.Summary, summary.Error, migration.Proposal, migration.Error);
        return new StoredAnalysis(entity.Id, entity.FileName, entity.Language, entity.Code, AsUtc(entity.CreatedAt), result);
    }

    private static FindingCounts ToCounts(IEnumerable<(Severity Severity, int Count)> groups)
    {
        var counts = groups.ToDictionary(g => g.Severity, g => g.Count);

        return new FindingCounts(
            counts.GetValueOrDefault(Severity.High),
            counts.GetValueOrDefault(Severity.Medium),
            counts.GetValueOrDefault(Severity.Low),
            counts.GetValueOrDefault(Severity.Info));
    }

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
