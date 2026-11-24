using LegacyLens.Core.Validation;

namespace LegacyLens.Core.Evaluation;

/// <summary>One compile check made through a <see cref="RecordingCodeValidator"/>.</summary>
public sealed record CodeCheck(string Code, CodeValidationResult Result);

/// <summary>Wraps an <see cref="ICodeValidator"/> and records every check, so the eval can keep the first attempt's code and errors.</summary>
public sealed class RecordingCodeValidator(ICodeValidator inner) : ICodeValidator
{
    private readonly List<CodeCheck> checks = [];

    /// <summary>Every check made so far, in order.</summary>
    public IReadOnlyList<CodeCheck> Checks => checks;

    public async Task<CodeValidationResult> ValidateAsync(string code, CancellationToken cancellationToken)
    {
        var result = await inner.ValidateAsync(code, cancellationToken);
        checks.Add(new CodeCheck(code, result));
        return result;
    }
}
