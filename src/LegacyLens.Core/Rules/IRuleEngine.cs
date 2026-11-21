using LegacyLens.Core.Model;

namespace LegacyLens.Core.Rules;

/// <summary>Runs all registered rules on a source file.</summary>
public interface IRuleEngine
{
    /// <summary>Returns the findings of all rules, ordered by line.</summary>
    IReadOnlyList<Finding> Run(SourceFile file);
}
