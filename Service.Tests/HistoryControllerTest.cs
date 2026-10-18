using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.History;
using NicheImageRipper.Service.Controllers;
using NicheImageRipper.Service.Models.Requests;

namespace NicheImageRipper.Service.Tests;

public class HistoryControllerTest
{
    private readonly FakeRipperSingleton _ripper = new();
    private HistoryController Controller => new(NullLogger<HistoryController>.Instance, _ripper);

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    public void OutOfRangePagingIsRejected(int start, int offset)
    {
        var response = Controller.GetHistory(start, offset, null, null);

        Assert.IsType<BadRequestResult>(response.Result);
        Assert.Null(_ripper.LastRequest);
    }

    [Fact]
    public void UnfilteredRequestPassesPaging()
    {
        var response = Controller.GetHistory(3, 25, null, null);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(_ripper.Entries, ok.Value);
        Assert.Equal((3, 25), (_ripper.LastRequest?.Start, _ripper.LastRequest?.Offset));
        Assert.Null(_ripper.LastRequest?.Filter);
    }

    [Fact]
    public void CountComesFromTheRipper()
    {
        var response = Controller.GetHistoryCount();

        Assert.Equal(42, Assert.IsType<OkObjectResult>(response.Result).Value);
    }

    public static TheoryData<HistoryFilter> Filters => new()
    {
        new HistoryNameFilter("Big Gallery & more"),
        new HistoryUrlFilter("https://example.com/a?b=c&d=e"),
        new HistoryDateFilter(new DateTime(2025, 2, 14, 13, 30, 0), HistoryFilterType.DateStart),
        new HistoryDateFilter(new DateTime(2024, 1, 1), HistoryFilterType.DateEnd),
    };

    /// <summary>
    ///     What the thin client sends (<see cref="GetHistoryRequest.ToQuery"/>) must come out as the same filter on
    ///     the service side.
    /// </summary>
    [Theory]
    [MemberData(nameof(Filters))]
    public void ClientQueryRoundTripsThroughTheController(HistoryFilter filter)
    {
        var query = new GetHistoryRequest { Start = 1, Offset = 50, Filter = filter }.ToQuery();
        var values = HttpUtility.ParseQueryString(query);

        Controller.GetHistory(int.Parse(values["start"]!), int.Parse(values["offset"]!),
            Enum.Parse<HistoryFilterType>(values["filterType"]!), values["filterValue"]);

        var received = _ripper.LastRequest?.Filter;
        Assert.NotNull(received);
        Assert.Equal(filter.GetType(), received.GetType());
        Assert.Equal(filter.FilterType, received.FilterType);
        Assert.Equal(Describe(filter), Describe(received));
        Assert.Equal((1, 50), (_ripper.LastRequest?.Start, _ripper.LastRequest?.Offset));
    }

    [Fact]
    public void QueryWithoutFilterHasOnlyPaging()
    {
        var values = HttpUtility.ParseQueryString(new GetHistoryRequest { Start = 0, Offset = 10 }.ToQuery());

        Assert.Equal(["start", "offset"], values.AllKeys);
    }

    private static string Describe(HistoryFilter filter) => filter switch
    {
        HistoryNameFilter name => name.Name,
        HistoryUrlFilter url => url.Url,
        HistoryDateFilter date => date.Date.ToString("O"),
        _ => throw new ArgumentOutOfRangeException(nameof(filter)),
    };

    private sealed class FakeRipperSingleton : NotImplementedRipperSingleton
    {
        public List<HistoryEntry> Entries { get; } = [];

        public (int Start, int Offset, HistoryFilter? Filter)? LastRequest { get; private set; }

        public override IEnumerable<HistoryEntry> GetHistory(int start, int offset, HistoryFilter? filter = null)
        {
            LastRequest = (start, offset, filter);
            return Entries;
        }

        public override int GetHistoryCount() => 42;
    }
}
