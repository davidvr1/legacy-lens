namespace LegacyLens.Core.Validation;

/// <summary>One compiler error. <see cref="Line"/> is 1-based, in the code that was checked.</summary>
public sealed record CodeDiagnostic(string Id, string Message, int Line);
