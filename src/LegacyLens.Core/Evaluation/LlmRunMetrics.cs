namespace LegacyLens.Core.Evaluation;

/// <summary>
/// The metrics of one run (every case once). A metric is null when its task was not run in any case.
/// </summary>
public sealed record LlmRunMetrics(
    double? FactRecall,
    double? HallucinationRate,
    double? CompileFirstAttempt,
    double? CompileAfterRepair,
    double? SourcesValid,
    double MeanCaseSeconds,
    int Errors);
