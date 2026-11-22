using LegacyLens.Core.Migration;

namespace LegacyLens.Core.Tests.Migration;

public sealed class MigrationReplyValidatorTests
{
    [Fact]
    public void Valid_reply_returns_null() => Assert.Null(MigrationReplyValidator.Validate(new("code", ["n"], ["s"])));

    [Fact]
    public void Empty_lists_are_valid() => Assert.Null(MigrationReplyValidator.Validate(new("code", [], [])));

    [Fact]
    public void Null_reply_is_rejected() => Assert.Equal("migration is required.", MigrationReplyValidator.Validate(null));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    [InlineData(null)]
    public void Blank_code_is_rejected(string? code)
        => Assert.Equal("code is required.", MigrationReplyValidator.Validate(new(code!, [], [])));

    [Fact]
    public void Null_notes_are_rejected()
        => Assert.Equal("notes must be an array of non-empty strings.", MigrationReplyValidator.Validate(new("c", null!, [])));

    [Fact]
    public void Blank_note_item_is_rejected()
        => Assert.Equal("notes must be an array of non-empty strings.", MigrationReplyValidator.Validate(new("c", ["ok", " "], [])));

    [Fact]
    public void Null_sources_are_rejected()
        => Assert.Equal("sources must be an array of non-empty strings.", MigrationReplyValidator.Validate(new("c", [], null!)));

    [Fact]
    public void Blank_source_item_is_rejected()
        => Assert.Equal("sources must be an array of non-empty strings.", MigrationReplyValidator.Validate(new("c", [], [""])));
}
