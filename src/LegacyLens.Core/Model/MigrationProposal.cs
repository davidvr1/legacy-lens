using LegacyLens.Core.Validation;

namespace LegacyLens.Core.Model;

/// <summary>
/// An AI-generated migration of a legacy file to ASP.NET Core. <see cref="Diagnostics"/> holds the remaining
/// compiler errors and is empty when <see cref="CompilesOk"/> is true.
/// </summary>
public sealed record MigrationProposal(
    string Code,
    IReadOnlyList<string> Notes,
    IReadOnlyList<MigrationSource> Sources,
    bool CompilesOk,
    IReadOnlyList<CodeDiagnostic> Diagnostics,
    bool Repaired);
