using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Core.Tests;

public class RuleEngineTests
{
    private sealed class FakeRule(string id, params Finding[] findings) : IRule
    {
        public string Id => id;

        public IReadOnlyList<Finding> Analyze(SourceFile file) => findings;
    }

    private static Finding MakeFinding(string ruleId, Severity severity, int line) =>
        new(ruleId, severity, line, "title", "explanation", "fix");

    private static SourceFile DummyFile() => new("test.asp", SourceLanguage.ClassicAsp, "x = 1");

    [Fact]
    public void Run_OrdersByLine_ThenSeverityDescending_ThenRuleIdAscending()
    {
        var ruleLine2 = new FakeRule("BBB001", MakeFinding("BBB001", Severity.Low, 2));
        var ruleLine1 = new FakeRule(
            "AAA001",
            MakeFinding("AAA001", Severity.High, 1),
            MakeFinding("AAA001", Severity.Low, 1));
        var otherLine1 = new FakeRule("ZZZ001", MakeFinding("ZZZ001", Severity.Low, 1));

        var engine = new RuleEngine(new IRule[] { ruleLine2, ruleLine1, otherLine1 });

        var result = engine.Run(DummyFile());

        Assert.Equal(4, result.Count);

        Assert.Equal(1, result[0].Line);
        Assert.Equal(Severity.High, result[0].Severity);
        Assert.Equal("AAA001", result[0].RuleId);

        Assert.Equal(1, result[1].Line);
        Assert.Equal(Severity.Low, result[1].Severity);
        Assert.Equal("AAA001", result[1].RuleId);

        Assert.Equal(1, result[2].Line);
        Assert.Equal(Severity.Low, result[2].Severity);
        Assert.Equal("ZZZ001", result[2].RuleId);

        Assert.Equal(2, result[3].Line);
    }

    [Fact]
    public void Run_MergesFindingsFromMultipleRules()
    {
        var ruleA = new FakeRule("AAA001", MakeFinding("AAA001", Severity.Info, 5));
        var ruleB = new FakeRule("BBB001", MakeFinding("BBB001", Severity.Info, 3));
        var engine = new RuleEngine(new IRule[] { ruleA, ruleB });

        var result = engine.Run(DummyFile());

        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.RuleId == "AAA001");
        Assert.Contains(result, f => f.RuleId == "BBB001");
    }

    [Fact]
    public void Run_NoRules_ReturnsEmpty()
    {
        var engine = new RuleEngine(Array.Empty<IRule>());

        Assert.Empty(engine.Run(DummyFile()));
    }

    [Fact]
    public void Run_NullFile_Throws()
    {
        var engine = new RuleEngine(Array.Empty<IRule>());

        Assert.Throws<ArgumentNullException>(() => engine.Run(null!));
    }
}
