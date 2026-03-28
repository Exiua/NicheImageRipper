using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Singletons;

namespace Service.Controllers;

[Authorize(AuthenticationSchemes = "ApiKey")]
[ApiController]
[Route("api/state")]
public class StateController(ILogger<StateController> logger, INicheImageRipperSingleton nicheImageRipperSingleton) : ControllerBase
{
    [HttpPost("rip")]
    public Task<ActionResult<bool>> Rip()
    {
        try
        {
            return Task.FromResult<ActionResult<bool>>(Ok(nicheImageRipperSingleton.Rip()));
        }
        catch (Exception exception)
        {
            return Task.FromException<ActionResult<bool>>(exception);
        }
    }

    [HttpPost("pause")]
    public Task<ActionResult<bool>> Pause()
    {
        try
        {
            return Task.FromResult<ActionResult<bool>>(Ok(nicheImageRipperSingleton.Pause()));
        }
        catch (Exception exception)
        {
            return Task.FromException<ActionResult<bool>>(exception);
        }
    }
    
    [HttpPost("resume")]
    public Task<ActionResult<bool>> Resume()
    {
        try
        {
            return Task.FromResult<ActionResult<bool>>(Ok(nicheImageRipperSingleton.Resume()));
        }
        catch (Exception exception)
        {
            return Task.FromException<ActionResult<bool>>(exception);
        }
    }

    [HttpGet("paused")]
    public ActionResult<bool> GetPausedState()
    {
        return Ok(nicheImageRipperSingleton.Paused);
    }

    [HttpGet("is-ripping")]
    public ActionResult<bool> GetIsRippingState()
    {
        return Ok(nicheImageRipperSingleton.IsRipping);
    }
}