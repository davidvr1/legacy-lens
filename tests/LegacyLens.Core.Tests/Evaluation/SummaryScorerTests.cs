using LegacyLens.Core.Evaluation;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Tests.Evaluation;

public sealed class SummaryScorerTests
{
    private const string Code = "id = Request.QueryString(\"orderId\")\nSet rs = Server.CreateObject(\"ADODB.Recordset\")\nSELECT * FROM OrderLines\n<!--#include file=\"db.inc\"-->";

    private static ExpectedFact F(SummaryCategory c, params string[] keywords) => new(c, keywords);

    private static CodeSummary S(
        string[]? inputs = null,
        string[]? outputs = null,
        string[]? db = null,
        string[]? includes = null,
        string[]? com = null,
        string purpose = "Anything at all.")
        => new(purpose, inputs ?? [], outputs ?? [], db ?? [], includes ?? [], com ?? []);

    private static SummaryScore Score(CodeSummary s, IReadOnlyList<ExpectedFact> facts, string code = Code)
        => SummaryScorer.Score(s, facts, code);

    [Fact]
    public void Full_recall_when_all_facts_stated()
    {
        var facts = new[] { F(SummaryCategory.Inputs, "QueryString", "orderId"), F(SummaryCategory.ComObjects, "ADODB.Recordset") };
        var score = Score(S(inputs: ["QueryString: orderId"], com: ["ADODB.Recordset"]), facts);
        Assert.Equal(2, score.ExpectedFacts);
        Assert.Equal(2, score.MatchedFacts);
        Assert.Equal(1.0, score.FactRecall);
        Assert.Empty(score.MissedFacts);
    }

    [Fact]
    public void Partial_recall_lists_missed_facts()
    {
        var missed = F(SummaryCategory.Includes, "db.inc");
        var score = Score(S(inputs: ["QueryString: orderId"]), [F(SummaryCategory.Inputs, "orderId"), missed]);
        Assert.Equal(1, score.MatchedFacts);
        Assert.Equal(0.5, score.FactRecall);
        Assert.Equal(missed, Assert.Single(score.MissedFacts));
    }

    [Fact]
    public void Recall_requires_matching_category()
    {
        var score = Score(S(outputs: ["orderId shown"]), [F(SummaryCategory.Inputs, "orderId")]);
        Assert.Equal(0, score.MatchedFacts);
        Assert.Equal(0.0, score.FactRecall);
    }

    [Fact]
    public void Recall_is_case_insensitive()
    {
        var score = Score(S(inputs: ["QUERYSTRING: ORDERID"]), [F(SummaryCategory.Inputs, "querystring", "orderid")]);
        Assert.Equal(1.0, score.FactRecall);
    }

    [Fact]
    public void Zero_facts_gives_recall_one()
    {
        var score = Score(S(), []);
        Assert.Equal(0, score.ExpectedFacts);
        Assert.Equal(1.0, score.FactRecall);
    }

    [Fact]
    public void Zero_stated_items_gives_hallucination_rate_zero()
    {
        var score = Score(S(), [F(SummaryCategory.Inputs, "x")]);
        Assert.Equal(0, score.StatedItems);
        Assert.Equal(0.0, score.HallucinationRate);
        Assert.Equal(0.0, score.FactRecall);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("None.")]
    [InlineData("N/A")]
    [InlineData("n/a.")]
    [InlineData("NA")]
    [InlineData("-")]
    [InlineData("Nothing")]
    [InlineData("no")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("  none  ")]
    [InlineData("Control: None")]
    [InlineData("Config: n/a.")]
    [InlineData("Session:")]
    public void Nothing_items_are_ignored_and_not_counted(string item)
    {
        var score = Score(S(inputs: [item], outputs: [item], db: [item], includes: [item], com: [item]), []);
        Assert.Equal(0, score.StatedItems);
        Assert.Equal(0, score.HallucinatedItems);
    }

    [Fact]
    public void Item_that_only_starts_with_none_is_counted()
    {
        var score = Score(S(inputs: ["none of the above zzzQwerty"]), []);
        Assert.Equal(1, score.StatedItems);
    }

    [Fact]
    public void Source_with_a_real_name_after_the_colon_is_counted()
    {
        var score = Score(S(inputs: ["QueryString: zzzQwerty"]), []);
        Assert.Equal(1, score.StatedItems);
        Assert.Equal(1, score.HallucinatedItems);
    }

    [Fact]
    public void Stated_items_count_all_five_lists_but_not_purpose()
    {
        var score = Score(S(["a1"], ["b1"], ["c1"], ["d1"], ["e1"], purpose: "Invented PaymentGateway purpose"), []);
        Assert.Equal(5, score.StatedItems);
    }

    [Fact]
    public void Supported_item_is_never_a_hallucination_even_if_not_in_code()
    {
        var score = Score(S(inputs: ["Cookie: zzzMissingToken"]), [F(SummaryCategory.Inputs, "zzzMissingToken")], code: "nothing here");
        Assert.Equal(0, score.HallucinatedItems);
        Assert.Empty(score.Hallucinations);
    }

    [Fact]
    public void Supported_requires_own_category()
    {
        var score = Score(S(outputs: ["zzzMissingToken"]), [F(SummaryCategory.Inputs, "zzzMissingToken")], code: "nothing here");
        Assert.Equal(1, score.HallucinatedItems);
    }

    [Fact]
    public void Item_grounded_in_code_is_not_a_hallucination()
    {
        var score = Score(S(db: ["Reads from OrderLines"]), []);
        Assert.Equal(1, score.StatedItems);
        Assert.Equal(0, score.HallucinatedItems);
    }

    [Fact]
    public void Grounding_is_case_insensitive()
    {
        var score = Score(S(db: ["reads ORDERLINES"]), []);
        Assert.Equal(0, score.HallucinatedItems);
    }

    [Fact]
    public void One_grounded_token_is_enough()
    {
        var score = Score(S(db: ["Reads zzzInvented and OrderLines"]), []);
        Assert.Equal(0, score.HallucinatedItems);
    }

    [Theory]
    [InlineData("The page data")]
    [InlineData("Reads the form values")]
    [InlineData("ab")]
    [InlineData("a b c")]
    [InlineData("SELECT from table")]
    public void Vague_items_with_only_generic_or_short_words_are_not_hallucinations(string item)
    {
        var score = Score(S(outputs: [item]), [], code: "completely unrelated");
        Assert.Equal(1, score.StatedItems);
        Assert.Equal(0, score.HallucinatedItems);
    }

    [Fact]
    public void Invented_name_is_a_hallucination_and_listed()
    {
        var score = Score(S(com: ["PaymentGateway.Client"]), []);
        Assert.Equal(1, score.HallucinatedItems);
        Assert.Equal("PaymentGateway.Client", Assert.Single(score.Hallucinations));
        Assert.Equal(1.0, score.HallucinationRate);
    }

    [Fact]
    public void Hallucination_text_is_trimmed()
    {
        var score = Score(S(com: ["  PaymentGateway  "]), []);
        Assert.Equal("PaymentGateway", Assert.Single(score.Hallucinations));
    }

    [Fact]
    public void Generic_words_do_not_ground_an_item()
    {
        // "querystring" and "form" are generic and appear in the code, but must not ground the item.
        var score = Score(S(inputs: ["QueryString zzzNotThere"]), [], code: "Request.QueryString Form");
        Assert.Equal(1, score.HallucinatedItems);
    }

    [Fact]
    public void Tokens_split_on_punctuation_and_keep_dots_and_underscores()
    {
        var grounded = Score(S(com: ["(ADODB.Recordset)"]), [], code: "x ADODB.Recordset y");
        Assert.Equal(0, grounded.HallucinatedItems);
        var snake = Score(S(inputs: ["order_id=5"]), [], code: "order_id");
        Assert.Equal(0, snake.HallucinatedItems);
        var bad = Score(S(inputs: ["order_id=5"]), [], code: "orderid");
        Assert.Equal(1, bad.HallucinatedItems);
    }

    [Fact]
    public void Leading_and_trailing_dots_are_trimmed_from_tokens()
    {
        var score = Score(S(db: ["reads OrderLines."]), []);
        Assert.Equal(0, score.HallucinatedItems);
    }

    [Fact]
    public void Rate_is_hallucinated_over_stated()
    {
        var score = Score(S(inputs: ["QueryString: orderId"], com: ["FakeThing", "AnotherFake"], includes: ["db.inc"]), [F(SummaryCategory.Inputs, "orderId")]);
        Assert.Equal(4, score.StatedItems);
        Assert.Equal(2, score.HallucinatedItems);
        Assert.Equal(0.5, score.HallucinationRate);
    }

    [Fact]
    public void Item_matching_a_fact_in_code_comment_still_judged_only_by_text()
    {
        // The scorer is plain substring on the raw code; a name only in a comment still grounds.
        var score = Score(S(com: ["LegacyMailer"]), [], code: "' LegacyMailer was removed");
        Assert.Equal(0, score.HallucinatedItems);
    }

    [Fact]
    public void GenericWords_is_case_insensitive_and_contains_contract_words()
    {
        Assert.Contains("QUERYSTRING", SummaryScorer.GenericWords);
        Assert.Contains("Recordset", SummaryScorer.GenericWords);
        Assert.Contains("none", SummaryScorer.GenericWords);
        Assert.DoesNotContain("orderid", SummaryScorer.GenericWords);
    }
}
