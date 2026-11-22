using LegacyLens.Core.Model;

namespace LegacyLens.Core.Migration;

/// <summary>Proposes a migration of a legacy source file to ASP.NET Core.</summary>
public interface IMigrationService
{
    Task<MigrationResult> ProposeAsync(SourceFile file, IReadOnlyList<Finding> findings, CancellationToken cancellationToken);
}
