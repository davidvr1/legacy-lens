using LegacyLens.Core.Retrieval;

namespace LegacyLens.Core.Tests.Retrieval;

public sealed class TokenizerTests
{
    [Fact]
    public void Contract_example()
    {
        Assert.Equal(
            ["server", "createobject", "create", "object", "adodb", "recordset"],
            Tokenizer.Tokenize("Server.CreateObject(\"ADODB.Recordset\")"));
    }

    [Theory]
    [InlineData("QueryString", new[] { "querystring", "query", "string" })]
    [InlineData("queryString", new[] { "querystring", "query", "string" })]
    [InlineData("HTMLEncode", new[] { "htmlencode", "html", "encode" })]
    [InlineData("ADODB", new[] { "adodb" })]
    [InlineData("recordset", new[] { "recordset" })]
    [InlineData("Recordset", new[] { "recordset" })]
    public void Camel_and_pascal_case_split(string input, string[] expected)
    {
        Assert.Equal(expected, Tokenizer.Tokenize(input));
    }

    [Fact]
    public void Splits_on_non_letter_non_digit_characters()
    {
        Assert.Equal(["rs", "movenext", "move", "next"], Tokenizer.Tokenize("rs.MoveNext;"));
        Assert.Equal(["snake", "case", "name"], Tokenizer.Tokenize("snake_case-name"));
    }

    [Fact]
    public void Output_is_lowercase()
    {
        Assert.All(Tokenizer.Tokenize("SELECT Id FROM Users WHERE NAME = 'X1'"), t => Assert.Equal(t.ToLowerInvariant(), t));
    }

    [Theory]
    [InlineData("the a an and or of to in is it for on with as by be this that from at are use not")]
    [InlineData("THE And Use NOT")]
    public void Stopwords_are_dropped(string input)
    {
        Assert.Empty(Tokenizer.Tokenize(input));
    }

    [Fact]
    public void Non_stopwords_survive_next_to_stopwords()
    {
        Assert.Equal(["session", "state"], Tokenizer.Tokenize("the session of state"));
    }

    [Fact]
    public void Tokens_shorter_than_two_chars_are_dropped()
    {
        Assert.Equal(["ab"], Tokenizer.Tokenize("a b ab x"));
        Assert.Empty(Tokenizer.Tokenize("x = 1 + y"));
    }

    [Fact]
    public void Digits_only_tokens_are_kept()
    {
        Assert.Equal(["404", "error"], Tokenizer.Tokenize("404 error"));
        Assert.Contains("10", Tokenizer.Tokenize("page 10"));
    }

    [Fact]
    public void Letters_and_digits_stay_in_one_piece()
    {
        Assert.Contains("sha256", Tokenizer.Tokenize("sha256"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \t\r\n")]
    [InlineData("!@#$%^&*()")]
    public void Empty_or_symbol_only_input_gives_no_tokens(string input)
    {
        Assert.Empty(Tokenizer.Tokenize(input));
    }

    [Fact]
    public void Null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Tokenizer.Tokenize(null!));
    }

    [Fact]
    public void Repeated_words_are_repeated_in_output()
    {
        Assert.Equal(["cat", "cat"], Tokenizer.Tokenize("cat cat"));
    }
}
