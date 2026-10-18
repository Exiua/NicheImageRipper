using System.Data.SQLite;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.History;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Core.Tests;

/// <summary>
///     Runs against a real database in a temp data folder. <see cref="HistoryManager"/> is a process-wide singleton,
///     so every test here shares it: each one uses its own names and URLs rather than assuming an empty table.
/// </summary>
[Collection(DataFolderCollection.Name)]
public class HistoryManagerTest
{
    private static HistoryManager History => HistoryManager.Instance;

    private static HistoryEntry Entry(string name, string url, DateTime date, int numUrls = 1) => new()
    {
        DirectoryName = name,
        Url = url,
        Date = date,
        NumUrls = numUrls,
    };

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public void DatabaseIsCreatedInTheDataFolder()
    {
        _ = History.GetHistoryEntryCount();

        Assert.True(File.Exists(AppData.GetPath("NicheImageRipper.db")));
    }

    [Fact]
    public void InsertedEntryCanBeFoundByUrlAndName()
    {
        var name = Unique("gallery");
        var url = $"https://example.com/{name}";
        var date = new DateTime(2024, 5, 6, 7, 8, 9);
        var countBefore = History.GetHistoryEntryCount();

        History.InsertHistoryRecord(Entry(name, url, date, numUrls: 12));

        var byUrl = History.GetHistoryByUrl(url);
        Assert.NotNull(byUrl);
        Assert.Equal(name, byUrl.DirectoryName);
        Assert.Equal(date, byUrl.Date);
        Assert.Equal(12, byUrl.NumUrls);
        Assert.Equal(url, History.GetHistoryEntryByDirectoryName(name)?.Url);
        Assert.True(History.UrlInHistory(url));
        Assert.Equal(countBefore + 1, History.GetHistoryEntryCount());
    }

    [Fact]
    public void UnknownUrlIsNotInHistory()
    {
        var url = $"https://example.com/{Unique("missing")}";

        Assert.False(History.UrlInHistory(url));
        Assert.Null(History.GetHistoryByUrl(url));
    }

    [Fact]
    public void PagesAreNewestFirst()
    {
        var token = Unique("paged");
        for (var i = 0; i < 5; i++)
        {
            History.InsertHistoryRecord(Entry($"{token}-{i}", $"https://example.com/{token}/{i}",
                DateTime.Now.AddMinutes(-i)));
        }

        var filter = new HistoryNameFilter(token);
        var first = History.GetHistory(0, 2, filter).Select(entry => entry.DirectoryName);
        var second = History.GetHistory(1, 2, filter).Select(entry => entry.DirectoryName);
        var last = History.GetHistory(2, 2, filter).Select(entry => entry.DirectoryName);

        Assert.Equal([$"{token}-0", $"{token}-1"], first);
        Assert.Equal([$"{token}-2", $"{token}-3"], second);
        Assert.Equal([$"{token}-4"], last);
    }

    [Fact]
    public void ShortNameSearchesFilter()
    {
        // Searches used to need more than 5 characters ("name:" plus one), even though the prefix is already
        // stripped, so a short search returned the whole history instead
        var token = $"zq{Guid.NewGuid().ToString("N")[..3]}"; // "z" never appears in other tests' hex names
        History.InsertHistoryRecord(Entry($"x{token}y", $"https://example.com/{Unique("short")}", DateTime.Now));
        History.InsertHistoryRecord(Entry(Unique("other"), $"https://example.com/{Unique("short")}", DateTime.Now));

        var results = History.GetHistory(0, 50, new HistoryNameFilter(token));

        Assert.Equal([$"x{token}y"], results.Select(entry => entry.DirectoryName));
    }

    [Fact]
    public void UrlSearchMatchesPartOfTheUrl()
    {
        var host = $"{Guid.NewGuid().ToString("N")[..3]}.test";
        History.InsertHistoryRecord(Entry(Unique("by-url"), $"https://{host}/gallery", DateTime.Now));

        var results = History.GetHistory(0, 50, new HistoryUrlFilter(host));

        Assert.Equal([$"https://{host}/gallery"], results.Select(entry => entry.Url));
    }

    [Fact]
    public void BlankSearchReturnsEverything()
    {
        History.InsertHistoryRecord(Entry(Unique("blank"), $"https://example.com/{Unique("blank")}", DateTime.Now));

        Assert.Equal(History.GetHistory(0, 1000).Count, History.GetHistory(0, 1000, new HistoryNameFilter(" ")).Count);
    }

    [Fact]
    public void DateFiltersBoundTheRange()
    {
        // Dates far in the past, so other tests' entries (dated now) stay out of the "before" results
        var token = Unique("dated");
        History.InsertHistoryRecord(Entry($"{token}-1991", $"https://example.com/{token}/1991", new DateTime(1991, 3, 1)));
        History.InsertHistoryRecord(Entry($"{token}-1992", $"https://example.com/{token}/1992", new DateTime(1992, 3, 1)));
        History.InsertHistoryRecord(Entry($"{token}-1993", $"https://example.com/{token}/1993", new DateTime(1993, 3, 1)));

        var before = History.GetHistory(0, 1000, new HistoryDateFilter(new DateTime(1992, 6, 1), HistoryFilterType.DateEnd))
                            .Select(entry => entry.DirectoryName).Where(name => name.StartsWith(token)).ToList();
        var after = History.GetHistory(0, 1000, new HistoryDateFilter(new DateTime(1992, 6, 1), HistoryFilterType.DateStart))
                           .Select(entry => entry.DirectoryName).Where(name => name.StartsWith(token)).ToList();

        Assert.Equal([$"{token}-1992", $"{token}-1991"], before);
        Assert.Equal([$"{token}-1993"], after);
    }

    [Fact]
    public void UrlAndDateCanBeUpdated()
    {
        var url = $"https://example.com/{Unique("update")}";
        History.InsertHistoryRecord(Entry(Unique("update"), url, new DateTime(2020, 1, 1)));

        var newDate = new DateTime(2021, 2, 3, 4, 5, 6);
        History.UpdateDateByUrl(url, newDate);

        Assert.Equal(newDate, History.GetHistoryByUrl(url)?.Date);
    }

    [Fact]
    public void MergeAddsNewEntriesAndKeepsTheLatestDate()
    {
        var existingUrl = $"https://example.com/{Unique("merge-existing")}";
        var newUrl = $"https://example.com/{Unique("merge-new")}";
        var staleUrl = $"https://example.com/{Unique("merge-stale")}";
        History.InsertHistoryRecord(Entry(Unique("existing"), existingUrl, new DateTime(2020, 1, 1)));
        History.InsertHistoryRecord(Entry(Unique("stale"), staleUrl, new DateTime(2022, 1, 1)));

        var other = Path.Combine(Path.GetTempPath(), $"nir-merge-{Guid.NewGuid():N}.db");
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={other}"))
            {
                connection.Open();
                Execute(connection, """
                                    CREATE TABLE history (
                                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                                        DirectoryName TEXT NOT NULL,
                                        Url TEXT NOT NULL,
                                        Date DATETIME NOT NULL,
                                        NumUrls INTEGER NOT NULL
                                    );
                                    """);
                Insert(connection, "existing-newer", existingUrl, new DateTime(2023, 1, 1));
                Insert(connection, "brand-new", newUrl, new DateTime(2023, 6, 1));
                Insert(connection, "stale-older", staleUrl, new DateTime(2019, 1, 1));
            }

            History.MergeHistory(other);
        }
        finally
        {
            SQLiteConnection.ClearAllPools();
            File.Delete(other);
        }

        Assert.Equal(new DateTime(2023, 1, 1), History.GetHistoryByUrl(existingUrl)?.Date);
        Assert.Equal("brand-new", History.GetHistoryByUrl(newUrl)?.DirectoryName);
        Assert.Equal(new DateTime(2022, 1, 1), History.GetHistoryByUrl(staleUrl)?.Date);
    }

    private static void Execute(SQLiteConnection connection, string sql)
    {
        using var command = new SQLiteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static void Insert(SQLiteConnection connection, string name, string url, DateTime date)
    {
        using var command = new SQLiteCommand(
            "INSERT INTO history (DirectoryName, Url, Date, NumUrls) VALUES (@Name, @Url, @Date, 1);", connection);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Url", url);
        command.Parameters.AddWithValue("@Date", date.ToSqliteString());
        command.ExecuteNonQuery();
    }
}
