using LegacyLens.Core.Model;

namespace LegacyLens.Core.Rules;

public sealed class RuleEngine(IEnumerable<IRule> rules) : IRuleEngine
{
    private readonly IReadOnlyList<IRule> _rules = rules.ToList();

    public IReadOnlyList<Finding> Run(SourceFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return _rules
            .SelectMany(rule => rule.Analyze(file))
            .OrderBy(finding => finding.Line)
            .ThenByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
            .ToList();
    }
}
