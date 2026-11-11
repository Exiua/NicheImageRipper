using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Service.Models.Requests;
using NicheImageRipper.Service.Models.Responses;
using NicheImageRipper.Service.Singletons;

namespace NicheImageRipper.Service.Controllers;

[Authorize(AuthenticationSchemes = "ApiKey")]
[ApiController]
[Route("api/queue")]
public class QueueController(ILogger<QueueController> logger, INicheImageRipperSingleton nicheImageRipperSingleton) : ControllerBase
{
    [HttpGet]
    public ActionResult<string[]> GetQueueSnapshot()
    {
        return Ok(nicheImageRipperSingleton.GetQueueSnapshot());
    }

    [HttpPost]
    public ActionResult<RejectedUrlsInfo> QueueUrls([FromBody] QueueRequest urls, [FromQuery] bool force = false)
    {
        logger.LogInformation("Queueing URLs (force: {Force})", force);
        logger.LogDebug("Queued URLs: {Urls}", urls.Urls);
        RejectedUrlsInfo rejected;
        if (force)
        {
            nicheImageRipperSingleton.ForceQueue(urls.Urls);
            rejected = new RejectedUrlsInfo(0);
        }
        else
        {
            rejected = nicheImageRipperSingleton.Queue(urls.Urls);
        }

        return Ok(rejected);
    }

    [HttpDelete]
    public IActionResult DequeueUrls([FromBody] string[] urls)
    {
        nicheImageRipperSingleton.Dequeue(urls);
        return Ok();
    }

    [HttpGet("count")]
    public ActionResult<long> GetQueueCount()
    {
        var queueSnapshot = nicheImageRipperSingleton.GetQueueSnapshot();
        return Ok(queueSnapshot.Length);
    }
    
    [HttpGet("current")]
    public ActionResult<CurrentUrlResponse> GetCurrentUrl()
    {
        return Ok(new CurrentUrlResponse(nicheImageRipperSingleton.GetCurrentUrl()));
    }

    [HttpGet("current/files")]
    public ActionResult<CurrentRipFiles> GetCurrentRipFiles()
    {
        return Ok(nicheImageRipperSingleton.GetCurrentRipFiles());
    }

    /// <summary>Saves the queue to a new unfinished-rip file on the backend and clears it.</summary>
    [HttpPost("save-and-clear")]
    public ActionResult<SavedQueueResponse> SaveQueueToFileAndClear()
    {
        var path = nicheImageRipperSingleton.SaveQueueToFileAndClear();
        logger.LogInformation("Saved queue to {Path} and cleared it", path);
        return Ok(new SavedQueueResponse(path));
    }

    [HttpPost("load")]
    public IActionResult LoadUrls([FromBody] List<string> urls)
    {
        nicheImageRipperSingleton.LoadUrls(urls);
        return Ok();
    }
}