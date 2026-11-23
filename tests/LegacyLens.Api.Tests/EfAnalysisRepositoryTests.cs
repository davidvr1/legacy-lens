using LegacyLens.Core.Analysis;
using LegacyLens.Core.Migration;
using LegacyLens.Core.Model;
using LegacyLens.Core.Summary;
using LegacyLens.Core.Validation;
using LegacyLens.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LegacyLens.Api.Tests;

/// <summary>Round trips of <see cref="EfAnalysisRepository"/> on an in-memory SQLite database.</summary>
public sealed class EfAnalysisRepositoryTests : IDisposable
{
    /// <summary>Returns a later time on every call, so saved analyses have distinct, known creation times.</summary>
    private sealed class StepTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _next = start;

        public override DateTimeOffset GetUtcNow()
        {
            var now = _next;
            _next = _next.AddMinutes(1);
            return now;
        }
    }

    private static readonly Finding HighFinding = new("SQL001", Severity.High, 4, "SQL from request data", "Use 'parameters' & check ünïcode", "Use SqlParameter");
    private static readonly Finding InfoFinding = new("COM001", Severity.Info, 12, "COM object", "Lists a COM dependency", "Replace with a .NET type");
    private static readonly Finding MediumFinding = new("ERR001", Severity.Medium, 2, "Error handling", "On Error Resume Next", "Check Err.Number");

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly LegacyLensDbContext _db;
    private readonly EfAnalysisRepository _repository;

    public EfAnalysisRepositoryTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<LegacyLensDbContext>().UseSqlite(_connection).Options;
        _db = new LegacyLensDbContext(options);
        _db.Database.EnsureCreated();
        _repository = new EfAnalysisRepository(_db, new StepTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static AnalysisResult FullResult() => new(
        [HighFinding, InfoFinding, MediumFinding],
        new CodeSummary(
            "Shows the orders of one customer",
            ["QueryString: customerId"],
            ["HTML table"],
            ["SELECT on Orders"],
            ["inc/db.asp"],
            ["Scripting.Dictionary"]),
        null,
        new MigrationProposal(
            "public class OrdersController { }",
            ["Use parameters", "Keep the view"],
            [new MigrationSource("sql-injection-parameterized-queries", "Parameterized queries")],
            false,
            [new CodeDiagnostic("CS1002", "; expected", 3), new CodeDiagnostic("CS0103", "The name 'x' does not exist", 7)],
            true),
        null);

    private static AnalysisResult ErrorsResult() => new(
        [HighFinding],
        null,
        SummaryService.UnavailableError,
        null,
        MigrationService.UnavailableError);

    private static SourceFile NewFile(string fileName = "orders.asp", SourceLanguage language = SourceLanguage.ClassicAsp) =>
        new(fileName, language, "<% Dim x %>");

    private Task<StoredAnalysis> Save(AnalysisResult result, string fileName = "orders.asp") =>
        _repository.SaveAsync(NewFile(fileName), result, CancellationToken.None);

    [Fact]
    public async Task Full_result_reads_back_equal_field_by_field()
    {
        var file = NewFile("orders.aspx.cs", SourceLanguage.WebForms);
        var expected = FullResult();

        var saved = await _repository.SaveAsync(file, expected, CancellationToken.None);
        var loaded = await _repository.GetAsync(saved.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(saved.Id, loaded!.Id);
        Assert.Equal(file.FileName, loaded.FileName);
        Assert.Equal(file.Language, loaded.Language);
        Assert.Equal(file.Code, loaded.Code);
        Assert.Equal(saved.CreatedAt, loaded.CreatedAt);
        AssertSameResult(expected, loaded.Result);
    }

    [Fact]
    public async Task Result_with_errors_and_no_summary_or_migration_reads_back_equal()
    {
        var expected = ErrorsResult();

        var saved = await Save(expected);
        var loaded = await _repository.GetAsync(saved.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        AssertSameResult(expected, loaded!.Result);
        Assert.Null(loaded.Result.Summary);
        Assert.Null(loaded.Result.Migration);
        Assert.Equal(SummaryService.UnavailableError, loaded.Result.SummaryError);
        Assert.Equal(MigrationService.UnavailableError, loaded.Result.MigrationError);

        var row = await _db.Analyses.AsNoTracking().SingleAsync(a => a.Id == saved.Id);
        Assert.Null(row.CompilesOk);
    }

    [Fact]
    public async Task Compiles_ok_is_stored_from_the_migration()
    {
        var failing = await Save(FullResult());
        var compiling = await Save(new AnalysisResult(
            [],
            null,
            null,
            new MigrationProposal("public class A { }", [], [], true, [], false),
            null));

        var failingRow = await _db.Analyses.AsNoTracking().SingleAsync(a => a.Id == failing.Id);
        var compilingRow = await _db.Analyses.AsNoTracking().SingleAsync(a => a.Id == compiling.Id);
        Assert.False(failingRow.CompilesOk);
        Assert.True(compilingRow.CompilesOk);
    }

    [Fact]
    public async Task Get_returns_null_for_an_unknown_id()
    {
        var loaded = await _repository.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task List_is_newest_first_limited_and_has_counts_per_severity()
    {
        var first = await Save(FullResult(), "first.asp");
        var second = await Save(ErrorsResult(), "second.asp");
        var third = await Save(new AnalysisResult([], null, null, null, null), "third.asp");

        var all = await _repository.ListAsync(10, CancellationToken.None);
        var limited = await _repository.ListAsync(2, CancellationToken.None);

        Assert.Equal([third.Id, second.Id, first.Id], all.Select(i => i.Id));
        Assert.Equal([third.Id, second.Id], limited.Select(i => i.Id));
        Assert.Equal(new FindingCounts(0, 0, 0, 0), all[0].FindingCounts);
        Assert.Equal(new FindingCounts(1, 0, 0, 0), all[1].FindingCounts);
        Assert.Equal(new FindingCounts(1, 1, 0, 1), all[2].FindingCounts);
        Assert.Equal("first.asp", all[2].FileName);
        Assert.Equal(first.CreatedAt, all[2].CreatedAt);
        Assert.Equal(SourceLanguage.ClassicAsp, all[2].Language);
    }

    private static void AssertSameResult(AnalysisResult expected, AnalysisResult actual)
    {
        Assert.Equal(expected.Findings, actual.Findings);
        Assert.Equal(expected.SummaryError, actual.SummaryError);
        Assert.Equal(expected.MigrationError, actual.MigrationError);

        Assert.Equal(expected.Summary is null, actual.Summary is null);
        if (expected.Summary is not null && actual.Summary is not null)
        {
            Assert.Equal(expected.Summary.Purpose, actual.Summary.Purpose);
            Assert.Equal(expected.Summary.Inputs, actual.Summary.Inputs);
            Assert.Equal(expected.Summary.Outputs, actual.Summary.Outputs);
            Assert.Equal(expected.Summary.DatabaseAccess, actual.Summary.DatabaseAccess);
            Assert.Equal(expected.Summary.Includes, actual.Summary.Includes);
            Assert.Equal(expected.Summary.ComObjects, actual.Summary.ComObjects);
        }

        Assert.Equal(expected.Migration is null, actual.Migration is null);
        if (expected.Migration is not null && actual.Migration is not null)
        {
            Assert.Equal(expected.Migration.Code, actual.Migration.Code);
            Assert.Equal(expected.Migration.Notes, actual.Migration.Notes);
            Assert.Equal(expected.Migration.Sources, actual.Migration.Sources);
            Assert.Equal(expected.Migration.CompilesOk, actual.Migration.CompilesOk);
            Assert.Equal(expected.Migration.Diagnostics, actual.Migration.Diagnostics);
            Assert.Equal(expected.Migration.Repaired, actual.Migration.Repaired);
        }
    }
}
