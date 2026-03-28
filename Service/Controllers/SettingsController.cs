using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Models.Configs;
using Service.Singletons;

namespace Service.Controllers;

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