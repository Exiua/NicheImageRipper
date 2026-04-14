using Core.DataStructures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Models.Requests;
using Service.Singletons;

namespace Service.Controllers;

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
    public ActionResult<RejectedUrlsInfo> QueueUrls(QueueRequest urls)
    {
        var rejected = nicheImageRipperSingleton.Queue(urls.Urls);
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
}