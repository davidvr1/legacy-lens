using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Core.Tests.Rules;

public class IncludeRuleTests
{
    private static IReadOnlyList<Finding> Analyze(string code)
    {
        var file = new SourceFile("test.asp", SourceLanguage.ClassicAsp, code);
        return new IncludeRule().Analyze(file);
    }

    [Fact]
    public void Flags_IncludeFile()
    {
        const string code = """<!--#include file="header.inc"-->""";

        var finding = Assert.Single(Analyze(code));

        Assert.Equal("INC001", finding.RuleId);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal(1, finding.Line);
        Assert.Contains("header.inc", finding.Title);
        Assert.Contains("#include file", finding.Explanation);
    }

    [Fact]
    public void Flags_IncludeVirtual()
    {
        const string code = """<!--#include virtual="/includes/footer.inc"-->""";

        var finding = Assert.Single(Analyze(code));

        Assert.Contains("/includes/footer.inc", finding.Title);
        Assert.Contains("#include virtual", finding.Explanation);
    }

    [Fact]
    public void Flags_MultipleIncludes_OnDifferentLines()
    {
        const string code = """
            <!--#include file="a.inc"-->
            <p>content</p>
            <!--#include virtual="/b.inc"-->
            """;

        var findings = Analyze(code);

        Assert.Equal(2, findings.Count);
        Assert.Equal(1, findings[0].Line);
        Assert.Equal(3, findings[1].Line);
    }

    [Fact]
    public void DoesNotFlag_UnsupportedAttributeName()
    {
        const string code = """<!--#include src="x.inc"-->""";

        Assert.Empty(Analyze(code));
    }
}
