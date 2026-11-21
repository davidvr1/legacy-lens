using LegacyLens.Core.Model;
using LegacyLens.Core.Rules.Parsing;

namespace LegacyLens.Core.Tests.Parsing;

public class StringLiteralsTests
{
    [Fact]
    public void Split_MaskedLine_SameLengthAsInput_ClassicAsp()
    {
        const string line = """sql = "SELECT * FROM Users WHERE id = " & id""";

        var result = StringLiterals.Split(line, SourceLanguage.ClassicAsp);

        Assert.Equal(line.Length, result.Masked.Length);
    }

    [Fact]
    public void Split_ExtractsLiteralContent_ClassicAsp()
    {
        const string line = """sql = "SELECT * FROM Users" """;

        var result = StringLiterals.Split(line, SourceLanguage.ClassicAsp);

        var literal = Assert.Single(result.Literals);
        Assert.Equal("SELECT * FROM Users", literal);
        Assert.DoesNotContain("SELECT", result.Masked);
    }

    [Fact]
    public void Split_MaskedLine_SameLengthAsInput_WebForms_WithEscapedQuote()
    {
        const string line = "string s = \"a\\\"b\" + x;";

        var result = StringLiterals.Split(line, SourceLanguage.WebForms);

        Assert.Equal(line.Length, result.Masked.Length);
    }

    [Fact]
    public void Split_WebFormsCharLiteral_NotTreatedAsStringStart()
    {
        const string line = "char c = '\\''; var s = \"text\";";

        var result = StringLiterals.Split(line, SourceLanguage.WebForms);

        Assert.Equal(line.Length, result.Masked.Length);
        var literal = Assert.Single(result.Literals);
        Assert.Equal("text", literal);
    }

    [Fact]
    public void Split_UnterminatedString_CapturesTrailingContent_SameLength()
    {
        const string line = "msg = \"unterminated";

        var result = StringLiterals.Split(line, SourceLanguage.ClassicAsp);

        Assert.Equal(line.Length, result.Masked.Length);
        var literal = Assert.Single(result.Literals);
        Assert.Equal("unterminated", literal);
    }
}
