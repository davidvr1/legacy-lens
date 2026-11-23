using System.Text.Json;
using LegacyLens.Core.Evaluation;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Tests.Evaluation;

public sealed class LlmEvalCaseParserTests
{
    private const string Valid = """
        { "language": "classic-asp",
          "facts": {
            "inputs": [["QueryString", "orderId"]],
            "outputs": [["html|table"]],
            "databaseAccess": [["Orders"]],
            "includes": [["db.inc"]],
            "comObjects": [["ADODB.Connection"], ["ADODB.Recordset"]]
          } }
        """;

    [Fact]
    public void Valid_file_parses_all_categories()
    {
        var c = LlmEvalCaseParser.Parse("case1", "input.asp", "CODE", Valid);
        Assert.Equal("case1", c.Id);
        Assert.Equal("input.asp", c.File.FileName);
        Assert.Equal("CODE", c.File.Code);
        Assert.Equal(SourceLanguage.ClassicAsp, c.File.Language);
        Assert.Equal(6, c.Facts.Count);
        Assert.Equal(2, c.Facts.Count(f => f.Category == SummaryCategory.ComObjects));
        var input = Assert.Single(c.Facts, f => f.Category == SummaryCategory.Inputs);
        Assert.Equal(["QueryString", "orderId"], input.Keywords);
        Assert.Equal(["html|table"], Assert.Single(c.Facts, f => f.Category == SummaryCategory.Outputs).Keywords);
        Assert.Single(c.Facts, f => f.Category == SummaryCategory.DatabaseAccess);
        Assert.Single(c.Facts, f => f.Category == SummaryCategory.Includes);
    }

    [Fact]
    public void Webforms_language()
    {
        var c = LlmEvalCaseParser.Parse("c", "input.aspx.cs", "x", """{ "language": "webforms", "facts": {} }""");
        Assert.Equal(SourceLanguage.WebForms, c.File.Language);
        Assert.Empty(c.Facts);
    }

    [Fact]
    public void Missing_categories_mean_no_facts()
    {
        var c = LlmEvalCaseParser.Parse("c", "f.asp", "x", """{ "language": "classic-asp", "facts": { "inputs": [["a"]] } }""");
        Assert.Equal(SummaryCategory.Inputs, Assert.Single(c.Facts).Category);
    }

    [Fact]
    public void Missing_facts_property_means_no_facts()
    {
        var c = LlmEvalCaseParser.Parse("c", "f.asp", "x", """{ "language": "classic-asp" }""");
        Assert.Empty(c.Facts);
    }

    [Theory]
    [InlineData("""{ "language": "php", "facts": {} }""")]
    [InlineData("""{ "language": "Classic-ASP", "facts": {} }""")]
    [InlineData("""{ "facts": {} }""")]
    public void Unknown_or_missing_language_throws(string json)
        => Assert.Throws<ArgumentException>(() => LlmEvalCaseParser.Parse("c", "f", "x", json));

    [Fact]
    public void Unknown_category_throws()
        => Assert.Throws<ArgumentException>(() => LlmEvalCaseParser.Parse("c", "f", "x",
            """{ "language": "classic-asp", "facts": { "cookies": [["a"]] } }"""));

    [Fact]
    public void Empty_keyword_list_throws()
        => Assert.Throws<ArgumentException>(() => LlmEvalCaseParser.Parse("c", "f", "x",
            """{ "language": "classic-asp", "facts": { "inputs": [[]] } }"""));

    [Theory]
    [InlineData("""[["a", ""]]""")]
    [InlineData("""[["  "]]""")]
    public void Empty_keyword_throws(string inputs)
        => Assert.Throws<ArgumentException>(() => LlmEvalCaseParser.Parse("c", "f", "x",
            "{ \"language\": \"classic-asp\", \"facts\": { \"inputs\": " + inputs + " } }"));

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    public void Malformed_json_throws_JsonException(string json)
        => Assert.ThrowsAny<JsonException>(() => LlmEvalCaseParser.Parse("c", "f", "x", json));
}
