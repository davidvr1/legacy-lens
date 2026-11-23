using LegacyLens.Core.Evaluation;

namespace LegacyLens.Core.Tests.Evaluation;

public sealed class ExpectedFactTests
{
    private static ExpectedFact Fact(params string[] keywords) => new(SummaryCategory.Inputs, keywords);

    [Fact]
    public void Single_keyword_present_matches() => Assert.True(Fact("orderId").Matches("QueryString: orderId"));

    [Fact]
    public void Single_keyword_absent_does_not_match() => Assert.False(Fact("orderId").Matches("QueryString: userId"));

    [Fact]
    public void Matching_is_case_insensitive() => Assert.True(Fact("QUERYSTRING", "ORDERID").Matches("querystring: orderid"));

    [Fact]
    public void All_keywords_are_required() => Assert.False(Fact("QueryString", "orderId").Matches("QueryString: other"));

    [Theory]
    [InlineData("an HTML page", true)]
    [InlineData("a TABLE of rows", true)]
    [InlineData("a list", false)]
    public void Alternatives_any_one_suffices(string item, bool expected)
        => Assert.Equal(expected, Fact("html|table").Matches(item));

    [Fact]
    public void Alternatives_combined_with_other_keywords_require_each_keyword()
    {
        var fact = Fact("Form|QueryString", "id");
        Assert.True(fact.Matches("QueryString: id"));
        Assert.True(fact.Matches("Form field id"));
        Assert.False(fact.Matches("Cookie: id"));
        Assert.False(fact.Matches("Form field name"));
    }

    [Fact]
    public void Substring_match_not_word_match() => Assert.True(Fact("order").Matches("orderId"));

    [Fact]
    public void Empty_item_does_not_match() => Assert.False(Fact("x").Matches(""));
}
