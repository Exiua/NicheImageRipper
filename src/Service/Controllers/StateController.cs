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
    public ActionResult<bool> Rip()
    {
        logger.LogInformation("Ripping");
        return Ok(nicheImageRipperSingleton.Rip());
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save()
    {
        logger.LogInformation("Saving");
        // Not tied to the request: a client disconnecting mustn't leave a half-written save behind
        await nicheImageRipperSingleton.Save(CancellationToken.None);
        return Ok();
    }

    [HttpPost("pause")]
    public ActionResult<bool> Pause()
    {
        logger.LogInformation("Pausing");
        return Ok(nicheImageRipperSingleton.Pause());
    }

    [HttpPost("resume")]
    public ActionResult<bool> Resume()
    {
        logger.LogInformation("Resuming");
        return Ok(nicheImageRipperSingleton.Resume());
    }

    [HttpPost("skip-file")]
    public ActionResult<bool> SkipCurrentFile()
    {
        logger.LogInformation("Skipping current file");
        return Ok(nicheImageRipperSingleton.SkipCurrentFile());
    }

    [HttpGet("paused")]
    public ActionResult<bool> GetPausedState()
    {
        var isPaused = nicheImageRipperSingleton.Paused;
        logger.LogDebug("IsPaused: {isPaused}", isPaused);
        return Ok(isPaused);
    }

    [HttpGet("is-ripping")]
    public ActionResult<bool> GetIsRippingState()
    {
        var isRipping = nicheImageRipperSingleton.IsRipping;
        logger.LogDebug("IsRipping: {isRipping}", isRipping);
        return Ok(isRipping);
    }

    [HttpPost("clear-cache")]
    public IActionResult ClearCache()
    {
        logger.LogInformation("Clearing cache");
        nicheImageRipperSingleton.ClearCache();
        return Ok();
    }
}
