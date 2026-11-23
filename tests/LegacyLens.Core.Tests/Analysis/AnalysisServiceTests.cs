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

    /// <summary>Keeps saved analyses in a list so tests can check what was saved.</summary>
    private sealed class FakeAnalysisRepository : IAnalysisRepository
    {
        public List<StoredAnalysis> Saved { get; } = [];

        public Task<StoredAnalysis> SaveAsync(SourceFile file, AnalysisResult result, CancellationToken cancellationToken)
        {
            var stored = new StoredAnalysis(Guid.NewGuid(), file.FileName, file.Language, file.Code, DateTimeOffset.UtcNow, result);
            Saved.Add(stored);
            return Task.FromResult(stored);
        }

        public Task<IReadOnlyList<AnalysisListItem>> ListAsync(int limit, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AnalysisListItem>>(Saved.Take(limit).Select(s => new AnalysisListItem(
                s.Id, s.FileName, s.Language, s.CreatedAt, FindingCounts.From(s.Result.Findings))).ToList());

        public Task<StoredAnalysis?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Saved.FirstOrDefault(s => s.Id == id));
    }

    private static readonly Finding F = new("SQL001", Severity.High, 3, "t", "e", "f");

    [Fact]
    public async Task Combines_findings_and_summary_and_passes_findings_to_summary()
    {
        var summary = SummaryTestData.Valid();
        var summaryService = new FakeSummaryService(new SummaryResult(summary, null));
        var service = new AnalysisService(new FakeRuleEngine([F]), summaryService, new FakeMigrationService(), new FakeAnalysisRepository());

        var stored = await service.AnalyzeAsync(SummaryTestData.File(), CancellationToken.None);

        Assert.Equal([F], stored.Result.Findings);
        Assert.Same(summary, stored.Result.Summary);
        Assert.Null(stored.Result.SummaryError);
        Assert.Equal([F], summaryService.ReceivedFindings);
    }

    [Fact]
    public async Task Findings_are_returned_when_summary_fails()
    {
        var service = new AnalysisService(
            new FakeRuleEngine([F]),
            new FakeSummaryService(new SummaryResult(null, SummaryService.UnavailableError)),
            new FakeMigrationService(),
            new FakeAnalysisRepository());

        var stored = await service.AnalyzeAsync(SummaryTestData.File(), CancellationToken.None);

        Assert.Equal([F], stored.Result.Findings);
        Assert.Null(stored.Result.Summary);
        Assert.Equal(SummaryService.UnavailableError, stored.Result.SummaryError);
    }

    [Fact]
    public async Task Result_is_saved_last_and_the_returned_analysis_carries_the_findings()
    {
        var repository = new FakeAnalysisRepository();
        var file = SummaryTestData.File();
        var service = new AnalysisService(
            new FakeRuleEngine([F]),
            new FakeSummaryService(new SummaryResult(SummaryTestData.Valid(), null)),
            new FakeMigrationService(),
            repository);

        var stored = await service.AnalyzeAsync(file, CancellationToken.None);

        var saved = Assert.Single(repository.Saved);
        Assert.Same(saved, stored);
        Assert.Equal(file.FileName, stored.FileName);
        Assert.Equal(file.Code, stored.Code);
        Assert.Equal(file.Language, stored.Language);
        Assert.Equal([F], stored.Result.Findings);
    }
}
