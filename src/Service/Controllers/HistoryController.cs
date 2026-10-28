using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.History;
using NicheImageRipper.Service.Singletons;

namespace NicheImageRipper.Service.Controllers;

[Authorize(AuthenticationSchemes = "ApiKey")]
[ApiController]
[Route("api/history")]
public partial class HistoryController(
    ILogger<HistoryController> logger,
    INicheImageRipperSingleton nicheImageRipperSingleton) : ControllerBase
{
    /// <summary>Largest page a client can ask for.</summary>
    public const int MaxPageSize = 1000;

    /// <param name="start">Zero-based page number</param>
    /// <param name="offset">Number of entries to return (the page size), at most <see cref="MaxPageSize"/></param>
    /// <param name="filterType">Optional filter; needs <paramref name="filterValue"/></param>
    /// <param name="filterValue">Filter argument: a date (ISO 8601) for date filters, otherwise text to match</param>
    [HttpGet]
    public ActionResult<IEnumerable<HistoryEntry>> GetHistory([FromQuery] int start,
                                                              [FromQuery] int offset,
                                                              [FromQuery] HistoryFilterType? filterType,
                                                              [FromQuery] string? filterValue)
    {
        if (start < 0 || offset < 1 || offset > MaxPageSize)
        {
            return BadRequest();
        }

        HistoryFilter? filter = null;
        if (filterType is { } type)
        {
            if (!TryCreateFilter(type, filterValue, out filter, out var error))
            {
                return BadRequest(error);
            }
        }

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

    private static bool TryCreateFilter(HistoryFilterType type, string? value, out HistoryFilter? filter,
                                        out string? error)
    {
        filter = null;
        error = null;
        if (string.IsNullOrEmpty(value))
        {
            error = $"filterValue is required with filterType {type}.";
            return false;
        }

        switch (type)
        {
            case HistoryFilterType.DateStart or HistoryFilterType.DateEnd:
                if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                        out var date))
                {
                    error = $"'{value}' is not a valid date.";
                    return false;
                }

                filter = new HistoryDateFilter(date, type);
                return true;
            case HistoryFilterType.Url:
                filter = new HistoryUrlFilter(value);
                return true;
            case HistoryFilterType.DirectoryName:
                filter = new HistoryNameFilter(value);
                return true;
            default:
                error = $"Unknown filterType {type}.";
                return false;
        }
    }

    [LoggerMessage(LogLevel.Debug, "GetHistoryCount: {count}")]
    static partial void LogHistoryCount(ILogger<HistoryController> logger, int count);
}
