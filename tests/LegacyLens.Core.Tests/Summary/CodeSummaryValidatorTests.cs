using System.Text.Json;
using LegacyLens.Core.Model;
using LegacyLens.Core.Summary;

namespace LegacyLens.Core.Tests.Summary;

public sealed class CodeSummaryValidatorTests
{
    private static CodeSummary Ok() => SummaryTestData.Valid();

    [Fact]
    public void Valid_summary_has_no_error() => Assert.Null(CodeSummaryValidator.Validate(Ok()));

    [Fact]
    public void Empty_lists_are_valid() =>
        Assert.Null(CodeSummaryValidator.Validate(new CodeSummary("p", [], [], [], [], [])));

    [Fact]
    public void Null_summary_is_invalid() => Assert.NotNull(CodeSummaryValidator.Validate(null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_purpose_is_invalid(string? purpose) =>
        Assert.Equal("purpose is required.", CodeSummaryValidator.Validate(Ok() with { Purpose = purpose! }));

    [Fact]
    public void Each_null_list_is_invalid_and_named()
    {
        Assert.Contains("inputs", CodeSummaryValidator.Validate(Ok() with { Inputs = null! })!);
        Assert.Contains("outputs", CodeSummaryValidator.Validate(Ok() with { Outputs = null! })!);
        Assert.Contains("databaseAccess", CodeSummaryValidator.Validate(Ok() with { DatabaseAccess = null! })!);
        Assert.Contains("includes", CodeSummaryValidator.Validate(Ok() with { Includes = null! })!);
        Assert.Contains("comObjects", CodeSummaryValidator.Validate(Ok() with { ComObjects = null! })!);
    }

    [Fact]
    public void Whitespace_or_null_item_is_invalid()
    {
        Assert.Contains("outputs", CodeSummaryValidator.Validate(Ok() with { Outputs = ["x", "  "] })!);
        Assert.Contains("comObjects", CodeSummaryValidator.Validate(Ok() with { ComObjects = [null!] })!);
    }

    [Fact]
    public void Schema_is_a_json_object_with_six_required_properties()
    {
        using var doc = JsonDocument.Parse(CodeSummarySchema.Json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        var required = doc.RootElement.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        foreach (var name in new[] { "purpose", "inputs", "outputs", "databaseAccess", "includes", "comObjects" })
        {
            Assert.Contains(name, required);
            Assert.True(doc.RootElement.GetProperty("properties").TryGetProperty(name, out _));
        }

        Assert.Equal(6, required.Count);
    }
}
