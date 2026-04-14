using Core.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Singletons;
using Config = Service.Models.Configs.Config;

namespace Service.Controllers;

[Authorize(AuthenticationSchemes = "ApiKey")]
[ApiController]
[Route("api/[controller]")]
public class SettingsController(ILogger<SettingsController> logger, INicheImageRipperSingleton nicheImageRipperSingleton) : ControllerBase
{
    [HttpGet]
    public ActionResult<GeneralConfig> GetConfig()
    {
        return Ok(nicheImageRipperSingleton.GetConfig());
    }

    [HttpPatch]
    public IActionResult UpdateConfig(Config config)
    {
        nicheImageRipperSingleton.UpdateConfig(config);
        return Ok();
    }
}