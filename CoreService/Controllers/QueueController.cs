using CoreService.Models.Dtos;
using CoreService.Singletons;
using Microsoft.AspNetCore.Mvc;

namespace CoreService.Controllers;

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
    public ActionResult<List<RejectedUrlInfoDto>> QueueUrls([FromBody] string[] urls)
    {
        var rejected = nicheImageRipperSingleton.Queue(urls);
        return Ok(rejected);
    }

    [HttpDelete]
    public IActionResult DequeueUrls([FromQuery] string[] urls)
    {
        nicheImageRipperSingleton.Dequeue(urls);
        return Ok();
    }
}