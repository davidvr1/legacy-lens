using LegacyLens.Core.Model;
using LegacyLens.Core.Retrieval;

namespace LegacyLens.Core.Tests.Retrieval;

public sealed class KnowledgeParserTests
{
    [Fact]
    public void Title_is_first_h1_line_and_content_is_whole_markdown()
    {
        const string md = "# Session State\n\nTags: a\n\n## Notes\n- x";
        var chunk = KnowledgeParser.Parse("session-state", md);

        Assert.Equal("session-state", chunk.Id);
        Assert.Equal("Session State", chunk.Title);
        Assert.Equal(md, chunk.Content);
    }

    [Fact]
    public void Title_is_trimmed_and_handles_crlf()
    {
        var chunk = KnowledgeParser.Parse("x", "# Padded Title   \r\n\r\nbody");
        Assert.Equal("Padded Title", chunk.Title);
    }

    [Fact]
    public void Title_is_found_after_leading_blank_lines()
    {
        Assert.Equal("Late", KnowledgeParser.Parse("x", "\n\n# Late\nbody").Title);
    }

    [Fact]
    public void Only_first_h1_counts()
    {
        Assert.Equal("First", KnowledgeParser.Parse("x", "# First\n# Second").Title);
    }

    [Theory]
    [InlineData("## Only H2\ntext")]
    [InlineData("#NoSpace\ntext")]
    [InlineData("text without heading")]
    [InlineData("")]
    [InlineData("   # indented is not a heading")]
    public void Without_h1_title_falls_back_to_id(string md)
    {
        Assert.Equal("fallback-id", KnowledgeParser.Parse("fallback-id", md).Title);
    }

    [Fact]
    public void Empty_h1_falls_back_to_id()
    {
        Assert.Equal("the-id", KnowledgeParser.Parse("the-id", "# \nbody").Title);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => KnowledgeParser.Parse(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => KnowledgeParser.Parse("x", null!));
    }

    [Fact]
    public async Task InMemoryKnowledgeBase_returns_chunks_in_given_order()
    {
        var chunks = new[] { new KnowledgeChunk("b", "B", "bb"), new KnowledgeChunk("a", "A", "aa") };
        var kb = new InMemoryKnowledgeBase(chunks);

        var result = await kb.GetChunksAsync(CancellationToken.None);

        Assert.Equal(["b", "a"], result.Select(c => c.Id));
    }

    [Fact]
    public async Task InMemoryKnowledgeBase_empty_gives_empty_list()
    {
        var kb = new InMemoryKnowledgeBase([]);
        Assert.Empty(await kb.GetChunksAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InMemoryKnowledgeBase_copies_the_source_sequence()
    {
        var source = new List<KnowledgeChunk> { new("a", "A", "x") };
        var kb = new InMemoryKnowledgeBase(source);
        source.Add(new KnowledgeChunk("b", "B", "y"));

        Assert.Single(await kb.GetChunksAsync(CancellationToken.None));
    }
}
