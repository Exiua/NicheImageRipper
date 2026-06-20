using Core.DataStructures;
using Core.History;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.History;
using Service.Models.Requests;
using Service.Singletons;

namespace Service.Controllers;

[Authorize(AuthenticationSchemes = "ApiKey")]
[ApiController]
[Route("api/history")]
public partial class HistoryController(
    ILogger<HistoryController> logger,
    INicheImageRipperSingleton nicheImageRipperSingleton) : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<HistoryEntry>> GetHistory([FromQuery] int start,
                                                              [FromQuery] int offset,
                                                              [FromQuery] HistoryFilterType? filterType,
                                                              [FromQuery] string? filterValue)
    {
        if (start < 0 || offset < 1)
        {
            return BadRequest();
        }

        HistoryFilter? filter = filterType switch
        {
            HistoryFilterType.DateStart => new HistoryDateFilter(DateTime.Parse(filterValue!),
                HistoryFilterType.DateStart),
            HistoryFilterType.DateEnd => new HistoryDateFilter(DateTime.Parse(filterValue!), HistoryFilterType.DateEnd),
            HistoryFilterType.Url => new HistoryUrlFilter(filterValue!),
            HistoryFilterType.DirectoryName => new HistoryNameFilter(filterValue!),
            null => null,
            _ => throw new InvalidOperationException("Unknown filter type")
        };

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