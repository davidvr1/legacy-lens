namespace LegacyLens.Core.Validation;

/// <summary>Checks that C# code compiles. The code is never run.</summary>
public interface ICodeValidator
{
    Task<CodeValidationResult> ValidateAsync(string code, CancellationToken cancellationToken);
}
