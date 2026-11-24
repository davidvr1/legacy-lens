using LegacyLens.Core.Validation;

namespace LegacyLens.Core.Evaluation;

/// <summary>
/// What happened inside one migration, so the repair step can be measured. FirstCode and FirstDiagnostics are the
/// first compiled reply. RepairError says why the repair reply was not used (null when it was used, or no repair ran).
/// </summary>
public sealed record MigrationTrace(
    string FirstCode,
    IReadOnlyList<CodeDiagnostic> FirstDiagnostics,
    bool FirstTruncated,
    bool RepairAttempted,
    string? RepairError);
