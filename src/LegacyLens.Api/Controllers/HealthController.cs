using Microsoft.AspNetCore.Mvc;

namespace LegacyLens.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse("ok"));
}

public sealed record HealthResponse(string Status);
