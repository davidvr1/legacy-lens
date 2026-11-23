using System.Text.Json;
using LegacyLens.Core.Evaluation.Rules;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Tests.Evaluation.Rules;

public sealed class RuleEvalCaseParserTests
{
    [Fact]
    public void Parses_classic_asp_case()
    {
        var c = RuleEvalCaseParser.Parse("c1", "code", """{"language":"classic-asp","findings":[{"ruleId":"SQL001","line":3},{"ruleId":"XSS001","line":5}]}""");

        Assert.Equal("c1", c.Id);
        Assert.Equal("code", c.Code);
        Assert.Equal(SourceLanguage.ClassicAsp, c.Language);
        Assert.Equal([new ExpectedRuleFinding("SQL001", 3), new ExpectedRuleFinding("XSS001", 5)], c.Expected);
    }

    [Fact]
    public void Parses_webforms_case()
    {
        var c = RuleEvalCaseParser.Parse("c2", "", """{"language":"webforms","findings":[{"ruleId":"SEC001","line":1}]}""");
        Assert.Equal(SourceLanguage.WebForms, c.Language);
        Assert.Single(c.Expected);
    }

    [Fact]
    public void Empty_findings_list_is_valid()
    {
        var c = RuleEvalCaseParser.Parse("c3", "", """{"language":"classic-asp","findings":[]}""");
        Assert.Empty(c.Expected);
    }

    [Theory]
    [InlineData("""{"findings":[]}""")]
    [InlineData("""{"language":"cobol","findings":[]}""")]
    [InlineData("""{"language":"Classic-ASP","findings":[]}""")]
    [InlineData("""{"language":"classic-asp"}""")]
    [InlineData("""{"language":"classic-asp","findings":null}""")]
    [InlineData("""{"language":"classic-asp","findings":[{"ruleId":"","line":1}]}""")]
    [InlineData("""{"language":"classic-asp","findings":[{"ruleId":"   ","line":1}]}""")]
    [InlineData("""{"language":"classic-asp","findings":[{"line":1}]}""")]
    [InlineData("""{"language":"classic-asp","findings":[{"ruleId":"SQL001","line":0}]}""")]
    [InlineData("""{"language":"classic-asp","findings":[{"ruleId":"SQL001","line":-2}]}""")]
    [InlineData("""{"language":"classic-asp","findings":[{"ruleId":"SQL001"}]}""")]
    [InlineData("""{"language":"classic-asp","findings":[null]}""")]
    [InlineData("null")]
    [InlineData("{not json")]
    public void Invalid_json_throws_JsonException(string json)
    {
        Assert.ThrowsAny<JsonException>(() => RuleEvalCaseParser.Parse("x", "", json));
    }
}
