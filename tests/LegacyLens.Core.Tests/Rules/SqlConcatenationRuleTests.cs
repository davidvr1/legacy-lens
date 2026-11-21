using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Core.Tests.Rules;

public class SqlConcatenationRuleTests
{
    private static IReadOnlyList<Finding> Analyze(string code, SourceLanguage language = SourceLanguage.ClassicAsp)
    {
        var file = new SourceFile("test.asp", language, code);
        return new SqlConcatenationRule().Analyze(file);
    }

    [Fact]
    public void Flags_ConcatenatedRequestData_ClassicAsp()
    {
        const string code = """sql = "SELECT * FROM Users WHERE name = '" & Request("name") & "'" """;

        var finding = Assert.Single(Analyze(code));

        Assert.Equal("SQL001", finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Equal(1, finding.Line);
        Assert.Contains("Request", finding.Explanation);
    }

    [Fact]
    public void Flags_SqlAppend_ToEarlierSqlVariable()
    {
        const string code = """
            sql = "SELECT * FROM Users WHERE name = '"
            sql = sql & Request.QueryString("name")
            """;

        var finding = Assert.Single(Analyze(code));

        Assert.Equal(2, finding.Line);
        Assert.Contains("Request", finding.Explanation);
    }

    [Fact]
    public void Flags_MultilineContinuation_SecondLine()
    {
        const string code = """
            sql = "SELECT * FROM Users " & _
                  "WHERE id = " & id
            """;

        var finding = Assert.Single(Analyze(code));

        Assert.Equal(2, finding.Line);
        Assert.Contains("id", finding.Explanation);
        Assert.DoesNotContain("Request data", finding.Explanation);
    }

    [Fact]
    public void Flags_CSharp_WebForms_PlusConcatenation_QueryStringIndexer()
    {
        const string code = """string sql = "SELECT * FROM Users WHERE id = " + Request.QueryString["id"];""";

        var finding = Assert.Single(Analyze(code, SourceLanguage.WebForms));

        Assert.Equal(1, finding.Line);
        Assert.Contains("Request", finding.Explanation);
    }

    [Fact]
    public void DoesNotFlag_ParameterizedQuery_SingleLiteralNoConcatenation()
    {
        const string code = """cmd.CommandText = "SELECT * FROM Users WHERE name = @name" """;

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_PureLiteralConcatenation_NoVariables()
    {
        const string code = """sql = "SELECT * FROM Users WHERE id = " & "1" """;

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_CIntSafeOperand()
    {
        const string code = """sql = sql & " AND age = " & CInt(ageText)""";

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_CommentedOutVbScript_Apostrophe()
    {
        const string code = """' sql = "SELECT * FROM Users WHERE name = " & Request("name")""";

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_CommentedOutVbScript_Rem()
    {
        const string code = """Rem sql = "SELECT * FROM Users WHERE name = " & Request("name")""";

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_CommentedOutCSharp()
    {
        const string code = """// string sql = "SELECT * FROM Users WHERE id = " + Request.QueryString["id"];""";

        Assert.Empty(Analyze(code, SourceLanguage.WebForms));
    }

    [Fact]
    public void DoesNotFlag_RequestWordInsideStringLiteral_NotFromRequest()
    {
        const string code = """sql = "SELECT * FROM Logs WHERE msg = 'Request failed'" & id""";

        var finding = Assert.Single(Analyze(code));

        Assert.DoesNotContain("Request data", finding.Explanation);
        Assert.Contains("id", finding.Explanation);
    }
}
