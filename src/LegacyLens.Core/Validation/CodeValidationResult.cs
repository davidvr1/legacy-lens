namespace LegacyLens.Core.Validation;

/// <summary>The outcome of checking a piece of code. <see cref="Success"/> is true when there are no errors.</summary>
public sealed record CodeValidationResult(bool Success, IReadOnlyList<CodeDiagnostic> Diagnostics);
