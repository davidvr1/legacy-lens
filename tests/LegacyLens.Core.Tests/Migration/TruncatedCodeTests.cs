using LegacyLens.Core.Migration;

namespace LegacyLens.Core.Tests.Migration;

public sealed class TruncatedCodeTests
{
    private const string Head = "public sealed class M : PageModel\n{\n    public async Task<IActionResult> OnPostAsync()\n    {\n";

    [Theory]
    // Real endings from the eval, where an unescaped quote ended the JSON string.
    [InlineData(Head + "        Email = Request.Form[")]
    [InlineData(Head + "        Keyword = keyword ?? Request.Cookies[")]
    [InlineData(Head + "        var name = await dataSource.ExecuteScalarAsync<string>(")]
    [InlineData("class A { void M() { var x = 1; }")]
    [InlineData("class A { void M() => Call(1,")]
    public void Code_that_stops_in_the_middle_is_truncated(string code)
        => Assert.True(TruncatedCode.IsTruncated(code));

    [Theory]
    [InlineData("class A { void M() { } }")]
    [InlineData("class A { }\npublic sealed record Row(int Id, string Name);")]
    [InlineData("class A { string M(int x) => $\"{x}\"; }\n\n")]
    [InlineData("")]
    [InlineData("   \n")]
    public void Complete_or_empty_code_is_not_truncated(string code)
        => Assert.False(TruncatedCode.IsTruncated(code));
}
