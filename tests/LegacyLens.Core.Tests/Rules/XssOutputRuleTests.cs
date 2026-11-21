using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Core.Tests.Rules;

public class XssOutputRuleTests
{
    private static IReadOnlyList<Finding> Analyze(string code, SourceLanguage language = SourceLanguage.ClassicAsp)
    {
        var file = new SourceFile("test.asp", language, code);
        return new XssOutputRule().Analyze(file);
    }

    [Fact]
    public void Flags_ResponseWrite_RequestQueryString_ClassicAsp()
    {
        const string code = """Response.Write(Request.QueryString("name"))""";

        var finding = Assert.Single(Analyze(code));

        Assert.Equal("XSS001", finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Equal(1, finding.Line);
        Assert.Contains("Request data", finding.Explanation);
    }

    [Fact]
    public void Flags_InlineExpression_RequestCall()
    {
        const string code = """<%= Request("name") %>""";

        var finding = Assert.Single(Analyze(code));

        Assert.Equal(1, finding.Line);
    }

    [Fact]
    public void Flags_InlineExpression_WhenSurroundingHtmlContainsQuotes()
    {
        // Regression: a quote in the surrounding HTML must not hide the real "<%=" that follows.
        const string code = """<h2>"<%= Request("name") %>"</h2>""";

        var finding = Assert.Single(Analyze(code));

        Assert.Equal(1, finding.Line);
        Assert.Contains("Request data", finding.Explanation);
    }

    [Fact]
    public void DoesNotFlag_HtmlEncodedOutput_ServerHTMLEncode()
    {
        const string code = """Response.Write(Server.HTMLEncode(Request("name")))""";

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_EncodedExpressionSyntax()
    {
        const string code = """<%: Request("name") %>""";

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_CIntConversion_Safe()
    {
        const string code = """Response.Write(CInt(Request("id")))""";

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void Flags_TaintedVariable_Propagation()
    {
        const string code = """
            name = Request("name")
            Response.Write(name)
            """;

        var finding = Assert.Single(Analyze(code));

        Assert.Equal(2, finding.Line);
        Assert.Contains("'name'", finding.Explanation);
        Assert.Contains("request data", finding.Explanation);
    }

    [Fact]
    public void DoesNotFlag_ReassignedVariable_UntaintsBeforeOutput()
    {
        const string code = """
            name = Request("name")
            name = "safe"
            Response.Write(name)
            """;

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_EncodedAssignment_DoesNotPropagateTaint()
    {
        const string code = """
            name = Server.HTMLEncode(Request("name"))
            Response.Write(name)
            """;

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void Flags_CSharp_QueryStringIndexer_ToString()
    {
        const string code = """Response.Write(Request.QueryString["name"].ToString());""";

        var finding = Assert.Single(Analyze(code, SourceLanguage.WebForms));

        Assert.Equal(1, finding.Line);
    }

    [Fact]
    public void DoesNotFlag_CommentedOutVbScript()
    {
        const string code = """' Response.Write Request("name")""";

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_CommentedOutCSharp()
    {
        const string code = """// Response.Write(Request.QueryString["name"]);""";

        Assert.Empty(Analyze(code, SourceLanguage.WebForms));
    }

    [Fact]
    public void DoesNotFlag_RequestWordInsideStringLiteral()
    {
        const string code = """Response.Write("Please enter Request info")""";

        Assert.Empty(Analyze(code));
    }
}
