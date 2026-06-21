using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}