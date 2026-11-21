using LegacyLens.Core.Model;

namespace LegacyLens.Core.Rules;

/// <summary>One deterministic detector.</summary>
public interface IRule
{
    /// <summary>Stable rule id, for example <c>SQL001</c>.</summary>
    string Id { get; }

    IReadOnlyList<Finding> Analyze(SourceFile file);
}
