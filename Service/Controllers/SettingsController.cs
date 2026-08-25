using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Service.Singletons;
using Sdk.Configuration;
using Config = NicheImageRipper.Service.Models.Configs.Config;

namespace NicheImageRipper.Service.Controllers;

[Authorize(AuthenticationSchemes = "ApiKey")]
[ApiController]
[Route("api/[controller]")]
public class SettingsController(ILogger<SettingsController> logger, INicheImageRipperSingleton nicheImageRipperSingleton) : ControllerBase
{
    [HttpGet]
    public ActionResult<GeneralConfig> GetConfig()
    {
        logger.LogInformation("Getting config");
        return Ok(nicheImageRipperSingleton.GetConfig());
    }

    [HttpPatch]
    public IActionResult UpdateConfig(Config config)
    {
        nicheImageRipperSingleton.UpdateConfig(config);
        return Ok();
    }
}