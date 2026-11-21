using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Core.Tests.Rules;

public class OnErrorResumeNextRuleTests
{
    private static IReadOnlyList<Finding> Analyze(string code)
    {
        var file = new SourceFile("test.asp", SourceLanguage.ClassicAsp, code);
        return new OnErrorResumeNextRule().Analyze(file);
    }

    [Fact]
    public void Flags_ResumeNext_NoCheck_EndOfFile()
    {
        const string code = """
            On Error Resume Next
            conn.Execute sql
            """;

        var finding = Assert.Single(Analyze(code));

        Assert.Equal("ERR001", finding.RuleId);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Equal(1, finding.Line);
    }

    [Fact]
    public void Flags_ResumeNext_RegionEndedByOnErrorGotoZero_NoCheck()
    {
        const string code = """
            On Error Resume Next
            conn.Execute sql
            On Error Goto 0
            """;

        var finding = Assert.Single(Analyze(code));

        Assert.Equal(1, finding.Line);
    }

    [Fact]
    public void DoesNotFlag_ResumeNext_WithErrNumberCheck()
    {
        const string code = """
            On Error Resume Next
            conn.Execute sql
            If Err.Number <> 0 Then
                Response.Write "DB error"
            End If
            """;

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_ResumeNext_WithIfErrCheck()
    {
        const string code = """
            On Error Resume Next
            x = 1 / 0
            If Not Err Then
                Response.Write "ok"
            End If
            """;

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void Flags_OnlySecondResumeNext_WhenFirstIsChecked()
    {
        const string code = """
            On Error Resume Next
            x = 1 / 0
            If Err.Number <> 0 Then Err.Clear
            On Error Resume Next
            y = 2 / 0
            """;

        var finding = Assert.Single(Analyze(code));

        Assert.Equal(4, finding.Line);
    }

    [Fact]
    public void DoesNotFlag_CommentedOutResumeNext()
    {
        const string code = """
            ' On Error Resume Next
            conn.Execute sql
            """;

        Assert.Empty(Analyze(code));
    }
}
