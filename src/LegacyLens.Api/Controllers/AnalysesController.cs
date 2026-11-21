using Microsoft.AspNetCore.Mvc;
using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public sealed class AnalysesController(IRuleEngine ruleEngine) : ControllerBase
{
    public const int MaxCodeLength = 200_000;

    [HttpPost]
    [ProducesResponseType(typeof(AnalysisResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    public ActionResult<AnalysisResponse> Create(AnalyzeRequest request)
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
        var findings = ruleEngine.Run(new SourceFile(fileName, language.Value, request.Code));

        // Id is not stored until persistence (Stage 6).
        return Ok(new AnalysisResponse(Guid.NewGuid(), fileName, languageName!, findings));
    }
}

public sealed record AnalyzeRequest(string? FileName, string? Language, string? Code);

public sealed record AnalysisResponse(Guid Id, string FileName, string Language, IReadOnlyList<Finding> Findings);

public sealed record ErrorResponse(string Error);
