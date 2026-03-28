using Core.DataStructures;
using CoreService.Models.Requests;
using CoreService.Singletons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreService.Controllers;

[Authorize(AuthenticationSchemes = "ApiKey")]
[ApiController]
[Route("api/history")]
public partial class HistoryController(ILogger<HistoryController> logger, INicheImageRipperSingleton nicheImageRipperSingleton) : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<HistoryEntry>> GetHistory([FromBody] GetHistoryRequest request)
    {
        var start = request.Start;
        var offset = request.Offset;
        if (start < 0 || offset < 1)
        {
            return BadRequest();
        }

        var filter = request.Filter;
        var historyEntries = nicheImageRipperSingleton.GetHistory(start, offset, filter);
        return Ok(historyEntries);
    }
    
    [HttpGet("count")]
    public ActionResult<int> GetHistoryCount()
    {
        var count = nicheImageRipperSingleton.GetHistoryCount();
        LogHistoryCount(logger, count);
        return Ok(count);
    }

    [LoggerMessage(LogLevel.Information, "GetHistoryCount: {count}")]
    static partial void LogHistoryCount(ILogger<HistoryController> logger, int count);
}