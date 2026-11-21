using LegacyLens.Core.Model;
using LegacyLens.Core.Rules.Parsing;

namespace LegacyLens.Core.Tests.Parsing;

public class CodeScannerTests
{
    [Fact]
    public void StripComments_KeepsLineCount()
    {
        var lines = new[] { "<%", "' comment", "code = 1", "%>" };

        var result = CodeScanner.StripComments(lines, SourceLanguage.ClassicAsp);

        Assert.Equal(lines.Length, result.Count);
    }

    [Fact]
    public void StripComments_HtmlOutsideCodeBlock_BecomesSpaces()
    {
        const string html = "Hello";
        const string code = "<%= x %>";
        const string trailer = "World";
        var line = html + code + trailer;

        var result = CodeScanner.StripComments(new[] { line }, SourceLanguage.ClassicAsp)[0];

        var expected = new string(' ', html.Length) + code + new string(' ', trailer.Length);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void StripComments_HtmlQuote_DoesNotHideLaterCodeOnSameLine()
    {
        // A quote in HTML (outside <% %>) must not be mistaken for the start of a string literal
        // that would swallow the real "<%=" that follows on the same line.
        const string html = "<h2>\"Hello</h2>";
        const string code = "<%= term %>";
        var line = html + code;

        var result = CodeScanner.StripComments(new[] { line }, SourceLanguage.ClassicAsp)[0];

        Assert.Equal(new string(' ', html.Length) + code, result);
    }

    [Fact]
    public void StripComments_PercentGreaterThan_EndsComment()
    {
        var lines = new[] { "<% ' secret = \"abc\" %>" };

        var result = CodeScanner.StripComments(lines, SourceLanguage.ClassicAsp);

        Assert.Equal("<% %>", result[0]);
    }

    [Fact]
    public void StripComments_RemComment_StrippedAtStatementStart()
    {
        var lines = new[] { "Rem this is a comment", "x = 1" };

        // No "<%" anywhere in the file: it is treated as a pure VBScript include, all code.
        var result = CodeScanner.StripComments(lines, SourceLanguage.ClassicAsp);

        Assert.Equal(string.Empty, result[0]);
        Assert.Equal("x = 1", result[1]);
    }

    [Fact]
    public void StripComments_CSharpLineComment_Stripped()
    {
        var lines = new[] { "// this is a comment", "int x = 1;" };

        var result = CodeScanner.StripComments(lines, SourceLanguage.WebForms);

        Assert.Equal(string.Empty, result[0]);
        Assert.Equal("int x = 1;", result[1]);
    }

    [Fact]
    public void StripComments_FileWithoutAnyCodeBlock_TreatsEverythingAsCode()
    {
        // A pure VBScript include has no <% %> at all; CodeScanner must not blank it as HTML.
        var lines = new[] { "sql = \"SELECT 1\"" };

        var result = CodeScanner.StripComments(lines, SourceLanguage.ClassicAsp);

        Assert.Equal(lines[0], result[0]);
    }
}
