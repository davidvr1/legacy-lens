using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Core.Tests.Rules;

public class ComObjectRuleTests
{
    private static IReadOnlyList<Finding> Analyze(string code)
    {
        var file = new SourceFile("test.asp", SourceLanguage.ClassicAsp, code);
        return new ComObjectRule().Analyze(file);
    }

    [Fact]
    public void Flags_AdoRecordset_WithServerPrefix()
    {
        const string code = """Set rs = Server.CreateObject("ADODB.Recordset")""";

        var finding = Assert.Single(Analyze(code));

        Assert.Equal("COM001", finding.RuleId);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal(1, finding.Line);
        Assert.Contains("ADODB.Recordset", finding.Title);
        Assert.Contains("ADO.NET", finding.Fix);
    }

    [Fact]
    public void Flags_FileSystemObject_WithoutServerPrefix()
    {
        const string code = """Set fso = CreateObject("Scripting.FileSystemObject")""";

        var finding = Assert.Single(Analyze(code));

        Assert.Contains("System.IO", finding.Fix);
    }

    [Fact]
    public void Flags_CdoMessage()
    {
        const string code = """Set mail = Server.CreateObject("CDO.Message")""";

        var finding = Assert.Single(Analyze(code));

        Assert.Contains("email service", finding.Fix);
    }

    [Fact]
    public void Flags_MsxmlDomDocument()
    {
        const string code = """Set xmlDoc = Server.CreateObject("MSXML2.DOMDocument")""";

        var finding = Assert.Single(Analyze(code));

        Assert.Contains("System.Xml", finding.Fix);
    }

    [Fact]
    public void Flags_UnknownProgId_GenericFix()
    {
        const string code = """Set obj = Server.CreateObject("Something.Custom")""";

        var finding = Assert.Single(Analyze(code));

        Assert.Contains("Find a .NET library", finding.Fix);
    }

    [Fact]
    public void DoesNotFlag_CommentedOut()
    {
        const string code = """' Set rs = Server.CreateObject("ADODB.Recordset")""";

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_CreateObjectText_InsideStringLiteral()
    {
        // The text only describes a CreateObject call inside a VBScript string; it is not an
        // actual call, and VBScript's "" quote-escaping happens to break the regex in that case
        const string code = """msg = "Example: Server.CreateObject(""ADODB.Recordset"")" """;

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_CreateObjectText_InHtmlOutsideCodeBlocks()
    {
        // HTML outside <% %> is ignored. (A file with no <% at all is treated as code, because
        // VBScript includes and code-behind files have no delimiters; this is a known limit.)
        const string code = """
            <% Dim x %>
            <p>Server.CreateObject("ADODB.Recordset") used here</p>
            """;

        Assert.Empty(Analyze(code));
    }
}
