using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NicheImageRipper.Service.Singletons;

namespace NicheImageRipper.Service.Controllers;
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
            logger.LogInformation("Ripping");
            return Task.FromResult<ActionResult<bool>>(Ok(nicheImageRipperSingleton.Rip()));
        }
        catch (Exception exception)
        {
            return Task.FromException<ActionResult<bool>>(exception);
        }
    }

    [HttpPost("save")]
    public Task<IActionResult> Save(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Saving");
        nicheImageRipperSingleton.Save(cancellationToken: cancellationToken);
        return Task.FromResult<IActionResult>(Ok());
    }

    [HttpPost("pause")]
    public Task<ActionResult<bool>> Pause()
    {
        try
        {
            logger.LogInformation("Pausing");
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
            logger.LogInformation("Resuming");
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
        var isPaused = nicheImageRipperSingleton.Paused;
        logger.LogInformation("IsPaused: {isPaused}", isPaused);
        return Ok(isPaused);
    }

    [HttpGet("is-ripping")]
    public ActionResult<bool> GetIsRippingState()
    {
        var isRipping = nicheImageRipperSingleton.IsRipping;
        logger.LogInformation("IsRipping: {isRipping}", isRipping);
        return Ok(isRipping);
    }

    [HttpPost("clear-cache")]
    public Task<IActionResult> ClearCache(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Clearing cache");
        nicheImageRipperSingleton.ClearCache();
        return Task.FromResult<IActionResult>(Ok());
    }
}