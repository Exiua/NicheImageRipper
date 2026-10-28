using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Service.Singletons;
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
        logger.LogDebug("Getting config");
        return Ok(nicheImageRipperSingleton.GetConfig());
    }

    [HttpGet("sites")]
    public ActionResult<List<string>> GetSiteNames()
    {
        return Ok(nicheImageRipperSingleton.GetSiteNames());
    }

    /// <summary>Applies and saves the given settings; omitted (null) settings are left as they are.</summary>
    [HttpPatch]
    public IActionResult UpdateConfig(Config config)
    {
        nicheImageRipperSingleton.UpdateConfig(config);
        logger.LogInformation("Settings updated");
        return Ok();
    }
}