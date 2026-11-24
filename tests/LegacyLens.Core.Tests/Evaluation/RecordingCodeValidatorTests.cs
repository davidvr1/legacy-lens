using LegacyLens.Core.Evaluation;
using LegacyLens.Core.Tests.Migration;

namespace LegacyLens.Core.Tests.Evaluation;

public sealed class RecordingCodeValidatorTests
{
    [Fact]
    public async Task Records_every_check_in_order_and_returns_the_inner_result()
    {
        var fail = MigrationTestData.Fail();
        var ok = MigrationTestData.Ok();
        var validator = new RecordingCodeValidator(new FakeCodeValidator(fail, ok));

        var first = await validator.ValidateAsync("a", CancellationToken.None);
        var second = await validator.ValidateAsync("b", CancellationToken.None);

        Assert.Same(fail, first);
        Assert.Same(ok, second);
        Assert.Equal([new CodeCheck("a", fail), new CodeCheck("b", ok)], validator.Checks);
    }
}
