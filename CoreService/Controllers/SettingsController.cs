using CoreService.Singletons;
using Microsoft.AspNetCore.Mvc;

namespace CoreService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController(ILogger<SettingsController> logger, INicheImageRipperSingleton nicheImageRipperSingleton) : ControllerBase
{
    
}