using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Models.Dtos;
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
    public ActionResult<List<RejectedUrlInfoDto>> QueueUrls([FromBody] string[] urls)
    {
        var rejected = nicheImageRipperSingleton.Queue(urls);
        return Ok(rejected);
    }

    [HttpDelete]
    public IActionResult DequeueUrls([FromBody] string[] urls)
    {
        nicheImageRipperSingleton.Dequeue(urls);
        return Ok();
    }
}