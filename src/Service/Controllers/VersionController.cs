using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NicheImageRipper.Service.Models.Responses;
using NicheImageRipper.Service.Singletons;

namespace NicheImageRipper.Service.Controllers;

[Authorize(AuthenticationSchemes = "ApiKey")]
[ApiController]
[Route("api/version")]
public class VersionController(ILogger<VersionController> logger, INicheImageRipperSingleton nicheImageRipperSingleton)
    : ControllerBase
{
    [HttpGet]
    public ActionResult<Version> GetCurrentVersion()
    {
        var coreVersion = nicheImageRipperSingleton.GetVersion();
        logger.LogInformation("Current version: {CoreVersion}", coreVersion);
        return Ok(coreVersion);
    }

    [HttpGet("latest")]
    public async Task<ActionResult<UpdateCheckResponse>> CheckForUpdate(CancellationToken cancellationToken)
    {
        var result = await nicheImageRipperSingleton.CheckForUpdate(cancellationToken);
        logger.LogInformation("Update check: current {CurrentVersion}, latest {LatestVersion}", result.CurrentVersion,
            result.LatestVersion);
        return Ok(result);
    }
}