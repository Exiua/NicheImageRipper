using NicheImageRipper.Core.History;

namespace NicheImageRipper.Core.Tests;

public class HistoryFilterTest
{
    [Fact]
    public void PlainTextSearchesByName()
    {
        var filter = Assert.IsType<HistoryNameFilter>(HistoryFilter.Parse("cats"));

        Assert.Equal("cats", filter.Name);
        Assert.Equal(HistoryFilterType.DirectoryName, filter.FilterType);
    }

    [Theory]
    [InlineData("name:cats", "cats")]
    [InlineData("NAME:Big Gallery", "Big Gallery")]
    public void NamePrefixIsStripped(string input, string expected)
    {
        Assert.Equal(expected, Assert.IsType<HistoryNameFilter>(HistoryFilter.Parse(input)).Name);
    }

    [Fact]
    public void UrlPrefixSearchesByUrl()
    {
        var filter = Assert.IsType<HistoryUrlFilter>(HistoryFilter.Parse("url:example.com"));

        Assert.Equal("example.com", filter.Url);
        Assert.Equal(HistoryFilterType.Url, filter.FilterType);
    }

    [Theory]
    [InlineData("date:2025", 2025, 1, 1)]
    [InlineData("date:2025/02", 2025, 2, 1)]
    [InlineData("date:2025-02", 2025, 2, 1)]
    [InlineData("date:02/2025", 2025, 2, 1)]
    [InlineData("date:2025/02/14", 2025, 2, 14)]
    [InlineData("date:02/14/2025", 2025, 2, 14)]
    public void PartialDatesFillInTheMissingParts(string input, int year, int month, int day)
    {
        var filter = Assert.IsType<HistoryDateFilter>(HistoryFilter.Parse(input));

        Assert.Equal(new DateTime(year, month, day), filter.Date);
        Assert.Equal(HistoryFilterType.DateStart, filter.FilterType);
    }

    [Fact]
    public void MonthOnlyMeansThisYear()
    {
        var filter = Assert.IsType<HistoryDateFilter>(HistoryFilter.Parse("date:03"));

        Assert.Equal(new DateTime(DateTime.Now.Year, 3, 1), filter.Date);
    }

    [Fact]
    public void BeforeAndAfterSetTheDirection()
    {
        Assert.Equal(HistoryFilterType.DateEnd,
            Assert.IsType<HistoryDateFilter>(HistoryFilter.Parse("before:2024")).FilterType);
        Assert.Equal(HistoryFilterType.DateStart,
            Assert.IsType<HistoryDateFilter>(HistoryFilter.Parse("after:2024")).FilterType);
    }

    [Theory]
    [InlineData("date:soon")]
    [InlineData("before:13/2024")]
    public void UnreadableDatesGiveNoFilter(string input)
    {
        Assert.Null(HistoryFilter.Parse(input));
    }

    [Fact]
    public void DateFilterRejectsNonDateTypes()
    {
        Assert.Throws<ArgumentException>(() => new HistoryDateFilter(DateTime.Now, HistoryFilterType.Url));
    }
}
