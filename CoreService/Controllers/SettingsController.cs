using CoreService.Models.Configs;
using CoreService.Singletons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreService.Controllers;

[Authorize(AuthenticationSchemes = "ApiKey")]
[ApiController]
[Route("api/[controller]")]
public class SettingsController(ILogger<SettingsController> logger, INicheImageRipperSingleton nicheImageRipperSingleton) : ControllerBase
{
    [HttpGet]
    public ActionResult<Config> GetConfig()
    {
        return Ok(nicheImageRipperSingleton.GetConfig());
    }
    
}