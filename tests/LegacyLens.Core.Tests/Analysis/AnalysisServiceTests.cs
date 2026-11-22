using LegacyLens.Core.Analysis;
using LegacyLens.Core.Migration;
using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;
using LegacyLens.Core.Summary;
using LegacyLens.Core.Tests.Summary;

namespace LegacyLens.Core.Tests.Analysis;

public sealed class AnalysisServiceTests
{
    private sealed class FakeRuleEngine(IReadOnlyList<Finding> findings) : IRuleEngine
    {
        public IReadOnlyList<Finding> Run(SourceFile file) => findings;
    }

    private sealed class FakeMigrationService : IMigrationService
    {
        public Task<MigrationResult> ProposeAsync(SourceFile file, IReadOnlyList<Finding> findings, CancellationToken cancellationToken)
            => Task.FromResult(new MigrationResult(null, MigrationService.UnavailableError));
    }

    private sealed class FakeSummaryService(SummaryResult result) : ISummaryService
    {
        public IReadOnlyList<Finding>? ReceivedFindings { get; private set; }

        public Task<SummaryResult> SummarizeAsync(SourceFile file, IReadOnlyList<Finding> findings, CancellationToken cancellationToken)
        {
            ReceivedFindings = findings;
            return Task.FromResult(result);
        }
    }

    private static readonly Finding F = new("SQL001", Severity.High, 3, "t", "e", "f");

    [Fact]
    public async Task Combines_findings_and_summary_and_passes_findings_to_summary()
    {
        var summary = SummaryTestData.Valid();
        var summaryService = new FakeSummaryService(new SummaryResult(summary, null));
        var service = new AnalysisService(new FakeRuleEngine([F]), summaryService, new FakeMigrationService());

        var result = await service.AnalyzeAsync(SummaryTestData.File(), CancellationToken.None);

        Assert.Equal([F], result.Findings);
        Assert.Same(summary, result.Summary);
        Assert.Null(result.SummaryError);
        Assert.Equal([F], summaryService.ReceivedFindings);
    }

    [Fact]
    public async Task Findings_are_returned_when_summary_fails()
    {
        var service = new AnalysisService(
            new FakeRuleEngine([F]),
            new FakeSummaryService(new SummaryResult(null, SummaryService.UnavailableError)),
            new FakeMigrationService());

        var result = await service.AnalyzeAsync(SummaryTestData.File(), CancellationToken.None);

        Assert.Equal([F], result.Findings);
        Assert.Null(result.Summary);
        Assert.Equal(SummaryService.UnavailableError, result.SummaryError);
    }
}
