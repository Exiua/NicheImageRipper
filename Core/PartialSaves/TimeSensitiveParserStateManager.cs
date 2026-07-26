using System.Data.SQLite;

namespace NicheImageRipper.Core.PartialSaves;

/// <summary>Persists the "last URL" TimeSensitiveHtmlParser needs to look up its cached image-links file when UpdateLinks runs against a freshly-constructed parser instance.</summary>
public sealed class TimeSensitiveParserStateManager
{
    private static readonly string ConnectionString = new SQLiteConnectionStringBuilder
    {
        DataSource = "NicheImageRipper.db",
    }.ToString();

    public static TimeSensitiveParserStateManager Instance { get; } = new();

    private readonly SQLiteConnection _connection = new(ConnectionString);
    private readonly Lock _databaseLock = new();

    private TimeSensitiveParserStateManager()
    {
        _connection.Open();
        const string createTableQuery = """
                                        CREATE TABLE IF NOT EXISTS time_sensitive_parser_state (
                                            ParserKey TEXT NOT NULL,
                                            Url TEXT NOT NULL,
                                            LastUrl TEXT NOT NULL,
                                            PRIMARY KEY (ParserKey, Url)
                                        );
                                        """;
        using var cmd = new SQLiteCommand(createTableQuery, _connection);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Records the last-fetched URL for a given parser type and rip URL, so a later UpdateLinks call (possibly on a freshly-constructed parser instance) can find its cached image links.</summary>
    public void StoreLastUrl(string parserKey, string ripUrl, string lastUrl)
    {
        lock (_databaseLock)
        {
            const string upsertQuery = """
                                       INSERT INTO time_sensitive_parser_state (ParserKey, Url, LastUrl)
                                       VALUES (@ParserKey, @Url, @LastUrl)
                                       ON CONFLICT (ParserKey, Url) DO UPDATE SET LastUrl = @LastUrl;
                                       """;
            using var cmd = new SQLiteCommand(upsertQuery, _connection);
            cmd.Parameters.AddWithValue("@ParserKey", parserKey);
            cmd.Parameters.AddWithValue("@Url", ripUrl);
            cmd.Parameters.AddWithValue("@LastUrl", lastUrl);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Looks up the last-fetched URL recorded for a given parser type and rip URL.</summary>
    /// <returns>The stored URL, or null if none has been recorded.</returns>
    public string? GetLastUrl(string parserKey, string ripUrl)
    {
        lock (_databaseLock)
        {
            const string selectQuery = """
                                       SELECT LastUrl FROM time_sensitive_parser_state
                                       WHERE ParserKey = @ParserKey AND Url = @Url;
                                       """;
            using var cmd = new SQLiteCommand(selectQuery, _connection);
            cmd.Parameters.AddWithValue("@ParserKey", parserKey);
            cmd.Parameters.AddWithValue("@Url", ripUrl);
            return (string?)cmd.ExecuteScalar();
        }
    }
}