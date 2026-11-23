using Microsoft.AspNetCore.Mvc;
using LegacyLens.Core.Analysis;
using LegacyLens.Core.Model;
using LegacyLens.Core.Summary;

namespace LegacyLens.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public sealed class AnalysesController(IAnalysisService analysisService, IAnalysisRepository repository) : ControllerBase
{
    public const int MaxCodeLength = 200_000;

    /// <summary>Matches the FileName column length in the database.</summary>
    public const int MaxFileNameLength = 260;

    public const int MaxHistoryItems = 100;

    [HttpPost]
    [ProducesResponseType(typeof(AnalysisResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    public async Task<ActionResult<AnalysisResponse>> Create(AnalyzeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new ErrorResponse("Code is required."));
        }

        if (request.Code.Length > MaxCodeLength)
        {
            return BadRequest(new ErrorResponse($"Code is too large. The limit is {MaxCodeLength} characters."));
        }

        var languageName = request.Language?.Trim().ToLowerInvariant();
        SourceLanguage? language = languageName switch
        {
            "classic-asp" => SourceLanguage.ClassicAsp,
            "webforms" => SourceLanguage.WebForms,
            _ => null,
        };

        if (language is null)
        {
            return BadRequest(new ErrorResponse("Language must be 'classic-asp' or 'webforms'."));
        }

        var fileName = string.IsNullOrWhiteSpace(request.FileName) ? "pasted-code" : request.FileName.Trim();
        if (fileName.Length > MaxFileNameLength)
        {
            return BadRequest(new ErrorResponse($"File name is too long. The limit is {MaxFileNameLength} characters."));
        }

        var stored = await analysisService.AnalyzeAsync(
            new SourceFile(fileName, language.Value, request.Code),
            cancellationToken);

        return Ok(ToResponse(stored));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AnalysisListItemResponse>), 200)]
    public async Task<ActionResult<IReadOnlyList<AnalysisListItemResponse>>> List(CancellationToken cancellationToken)
    {
        var items = await repository.ListAsync(MaxHistoryItems, cancellationToken);

        return Ok(items.Select(item => new AnalysisListItemResponse(
            item.Id,
            item.FileName,
            ToLanguageName(item.Language),
            item.CreatedAt,
            item.FindingCounts)).ToList());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AnalysisResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<ActionResult<AnalysisResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var stored = await repository.GetAsync(id, cancellationToken);
        if (stored is null)
        {
            return NotFound(new ErrorResponse("Analysis not found."));
        }

        return Ok(ToResponse(stored));
    }

    private static AnalysisResponse ToResponse(StoredAnalysis stored) => new(
        stored.Id,
        stored.FileName,
        ToLanguageName(stored.Language),
        stored.CreatedAt,
        stored.Result.Findings,
        stored.Result.Summary,
        stored.Result.SummaryError,
        stored.Result.Migration,
        stored.Result.MigrationError);

    private static string ToLanguageName(SourceLanguage language) => language switch
    {
        SourceLanguage.ClassicAsp => "classic-asp",
        SourceLanguage.WebForms => "webforms",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown source language."),
    };
}

public sealed record AnalyzeRequest(string? FileName, string? Language, string? Code);

public sealed record AnalysisResponse(
    Guid Id,
    string FileName,
    string Language,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Finding> Findings,
    CodeSummary? Summary,
    string? SummaryError,
    MigrationProposal? Migration,
    string? MigrationError);

public sealed record AnalysisListItemResponse(
    Guid Id,
    string FileName,
    string Language,
    DateTimeOffset CreatedAt,
    FindingCounts FindingCounts);

public sealed record ErrorResponse(string Error);
