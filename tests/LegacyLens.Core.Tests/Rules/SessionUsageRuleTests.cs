using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Core.Tests.Rules;

public class SessionUsageRuleTests
{
    private static IReadOnlyList<Finding> Analyze(string code, int threshold = SessionUsageRule.DefaultThreshold)
    {
        var file = new SourceFile("test.asp", SourceLanguage.ClassicAsp, code);
        return new SessionUsageRule(threshold).Analyze(file);
    }

    private static string SessionAccessLines(int count, string[]? keys = null)
    {
        keys ??= ["k"];
        var lines = Enumerable.Range(0, count)
            .Select(i => $"""v{i} = Session("{keys[i % keys.Length]}")""");
        return string.Join('\n', lines);
    }

    [Fact]
    public void DoesNotFlag_AtThreshold()
    {
        var code = SessionAccessLines(SessionUsageRule.DefaultThreshold);

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void Flags_AboveThreshold()
    {
        var code = SessionAccessLines(SessionUsageRule.DefaultThreshold + 1);

        var finding = Assert.Single(Analyze(code));

        Assert.Equal("SES001", finding.RuleId);
        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Equal(1, finding.Line);
    }

    [Fact]
    public void DoesNotFlag_ZeroAccesses()
    {
        const string code = "x = 1";

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_CustomThreshold_AtBoundary()
    {
        var code = SessionAccessLines(2);

        Assert.Empty(Analyze(code, threshold: 2));
    }

    [Fact]
    public void Flags_CustomThreshold_JustAboveBoundary()
    {
        var code = SessionAccessLines(3);

        Assert.Single(Analyze(code, threshold: 2));
    }

    [Fact]
    public void Constructor_NegativeThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionUsageRule(-1));
    }

    [Fact]
    public void Finding_ReportsFirstAccessLine_AndDistinctKeys()
    {
        var code = SessionAccessLines(SessionUsageRule.DefaultThreshold + 1, ["user", "cart"]);

        var finding = Assert.Single(Analyze(code));

        Assert.Equal(1, finding.Line);
        Assert.Contains("11 times", finding.Explanation);
        Assert.Contains("2 keys", finding.Explanation);
        Assert.Contains("user", finding.Explanation);
        Assert.Contains("cart", finding.Explanation);
    }

    [Fact]
    public void DoesNotCount_SessionTextInsideStringLiteral()
    {
        // 10 real accesses (at the default threshold) plus one line whose "Session(" text is
        // entirely inside string literals must still not cross the threshold.
        var real = SessionAccessLines(SessionUsageRule.DefaultThreshold);
        const string fake = """greeting = "Session(" & "abc" & ")" """;
        var code = real + "\n" + fake;

        Assert.Empty(Analyze(code));
    }
}
