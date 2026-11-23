using LegacyLens.Core.Llm;
using LegacyLens.Core.Migration;
using LegacyLens.Core.Summary;
using LegacyLens.Infrastructure.Llm;
using LegacyLens.Infrastructure.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegacyLens.Api.Tests;

public sealed class PromptVariantTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddLlm(config).BuildServiceProvider();
    }

    [Fact]
    public async Task Few_shot_example_code_compiles()
    {
        var result = await new RoslynCodeValidator().ValidateAsync(MigrationPromptBuilder.FewShotExampleCode, CancellationToken.None);

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => $"{d.Line} {d.Id} {d.Message}")));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void FewShot_is_bound_into_summary_and_migration_settings()
    {
        using var sp = Build(new() { ["Llm:PromptVariant"] = "FewShot", ["Retrieval:TopK"] = "5" });

        Assert.Equal(PromptVariant.FewShot, sp.GetRequiredService<SummarySettings>().Variant);
        var migration = sp.GetRequiredService<MigrationSettings>();
        Assert.Equal(PromptVariant.FewShot, migration.Variant);
        Assert.Equal(5, migration.TopK);
    }

    [Fact]
    public void Default_variant_is_ZeroShot()
    {
        using var sp = Build([]);

        Assert.Equal(PromptVariant.ZeroShot, sp.GetRequiredService<SummarySettings>().Variant);
        Assert.Equal(PromptVariant.ZeroShot, sp.GetRequiredService<MigrationSettings>().Variant);
    }

    [Fact]
    public void ZeroShot_explicit_is_bound()
    {
        using var sp = Build(new() { ["Llm:PromptVariant"] = "ZeroShot" });
        Assert.Equal(PromptVariant.ZeroShot, sp.GetRequiredService<SummarySettings>().Variant);
    }
}
