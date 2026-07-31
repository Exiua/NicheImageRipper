using System.Data;
using System.Data.SQLite;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;

namespace NicheImageRipper.Core.PartialSaves;

/// <summary>
///     Persists the cached link list a <see cref="TimeSensitiveHtmlParser"/> needs across instances,
///     keyed by the parser's <c>ParserKey</c> and the original rip URL (parsers are recreated between the
///     initial parse and a later link-refresh, so this cannot live on the instance itself).
/// </summary>
public sealed class TimeSensitiveParserStateManager
{
    private static readonly string ConnectionString = new SQLiteConnectionStringBuilder
    {
        DataSource = "NicheImageRipper.db",
        ForeignKeys = true,
    }.ToString();

    /// <summary>The shared singleton instance.</summary>
    public static TimeSensitiveParserStateManager Instance { get; } = new();

    private readonly SQLiteConnection _connection = new(ConnectionString);
    private readonly Lock _databaseLock = new();

    private TimeSensitiveParserStateManager()
    {
        _connection.Open();

        const string createStateTableQuery = """
                                              CREATE TABLE IF NOT EXISTS time_sensitive_parser_state (
                                                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                  ParserKey TEXT NOT NULL,
                                                  Url TEXT NOT NULL,
                                                  UNIQUE (ParserKey, Url)
                                              );
                                              """;
        const string createLinksTableQuery = """
                                             CREATE TABLE IF NOT EXISTS time_sensitive_parser_links (
                                                 id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                 ParserStateId INTEGER NOT NULL,
                                                 LinkIndex INTEGER NOT NULL,
                                                 Link TEXT NOT NULL,
                                                 FOREIGN KEY (ParserStateId) REFERENCES time_sensitive_parser_state(id) ON DELETE CASCADE
                                             );
                                             """;
        const string createIndexQuery =
            "CREATE INDEX IF NOT EXISTS parser_state_id_index ON time_sensitive_parser_links (ParserStateId);";

        foreach (var query in new[] { createStateTableQuery, createLinksTableQuery, createIndexQuery })
        {
            using var cmd = new SQLiteCommand(query, _connection);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    ///     Records the link list for a given parser type and rip URL, replacing any previously stored links
    ///     for the same (parserKey, ripUrl) pair.
    /// </summary>
    /// <param name="parserKey">The parser's <c>ParserKey</c>.</param>
    /// <param name="ripUrl">The URL originally given to <c>Rip</c>/<c>ParseSite</c>.</param>
    /// <param name="links">The ordered link list to cache.</param>
    public void StoreLinks(string parserKey, string ripUrl, IReadOnlyList<string> links)
    {
        lock (_databaseLock)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                var stateId = UpsertState(parserKey, ripUrl, transaction);

                const string deleteLinksQuery = """
                                                DELETE FROM time_sensitive_parser_links
                                                WHERE ParserStateId = @ParserStateId;
                                                """;
                using (var deleteCmd = new SQLiteCommand(deleteLinksQuery, _connection, transaction))
                {
                    deleteCmd.Parameters.AddWithValue("@ParserStateId", stateId);
                    deleteCmd.ExecuteNonQuery();
                }

                const string insertLinkQuery = """
                                               INSERT INTO time_sensitive_parser_links (ParserStateId, LinkIndex, Link)
                                               VALUES (@ParserStateId, @LinkIndex, @Link);
                                               """;
                using var insertCmd = new SQLiteCommand(insertLinkQuery, _connection, transaction);
                var stateIdParam = insertCmd.Parameters.Add("@ParserStateId", DbType.Int64);
                var indexParam = insertCmd.Parameters.Add("@LinkIndex", DbType.Int32);
                var linkParam = insertCmd.Parameters.Add("@Link", DbType.String);
                stateIdParam.Value = stateId;
                for (var i = 0; i < links.Count; i++)
                {
                    indexParam.Value = i;
                    linkParam.Value = links[i];
                    insertCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    private long UpsertState(string parserKey, string ripUrl, SQLiteTransaction transaction)
    {
        const string upsertQuery = """
                                   INSERT INTO time_sensitive_parser_state (ParserKey, Url)
                                   VALUES (@ParserKey, @Url)
                                   ON CONFLICT (ParserKey, Url) DO NOTHING;

                                   SELECT id FROM time_sensitive_parser_state
                                   WHERE ParserKey = @ParserKey AND Url = @Url;
                                   """;
        using var cmd = new SQLiteCommand(upsertQuery, _connection, transaction);
        cmd.Parameters.AddWithValue("@ParserKey", parserKey);
        cmd.Parameters.AddWithValue("@Url", ripUrl);
        return (long)cmd.ExecuteScalar()!;
    }

    /// <summary>Looks up the cached link list for a given parser type and rip URL.</summary>
    /// <param name="parserKey">The parser's <c>ParserKey</c>.</param>
    /// <param name="ripUrl">The URL originally given to <c>Rip</c>/<c>ParseSite</c>.</param>
    /// <returns>The cached links in original order, or null if no cached state exists for this (parserKey, ripUrl) pair.</returns>
    public List<string>? GetLinks(string parserKey, string ripUrl)
    {
        lock (_databaseLock)
        {
            const string selectStateIdQuery = """
                                              SELECT id FROM time_sensitive_parser_state
                                              WHERE ParserKey = @ParserKey AND Url = @Url;
                                              """;
            using var stateIdCmd = new SQLiteCommand(selectStateIdQuery, _connection);
            stateIdCmd.Parameters.AddWithValue("@ParserKey", parserKey);
            stateIdCmd.Parameters.AddWithValue("@Url", ripUrl);
            var stateId = stateIdCmd.ExecuteScalar();
            if (stateId is null)
            {
                return null;
            }

            const string selectLinksQuery = """
                                            SELECT Link FROM time_sensitive_parser_links
                                            WHERE ParserStateId = @ParserStateId
                                            ORDER BY LinkIndex;
                                            """;
            using var linksCmd = new SQLiteCommand(selectLinksQuery, _connection);
            linksCmd.Parameters.AddWithValue("@ParserStateId", stateId);
            using var reader = linksCmd.ExecuteReader();

            var links = new List<string>();
            while (reader.Read())
            {
                links.Add(reader.GetString(0));
            }

            return links;
        }
    }
}