using System.Data;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.ExtensionMethods;
using Serilog;

namespace NicheImageRipper.Core.History;

/// <summary>
///     Singleton manager for the persistent rip history stored in the shared SQLite database. All public
///     members that touch the underlying connection are synchronized via a single lock, since
///     <see cref="SQLiteConnection"/> is not safe for concurrent use from multiple threads.
/// </summary>
public class HistoryManager : IDisposable
{
    private const string ConnectionString = "Data Source=NicheImageRipper.db";

    private static readonly ILogger Logger = Log.ForContext<HistoryManager>();

    /// <summary>The shared singleton instance.</summary>
    public static HistoryManager Instance { get; } = new();

    private readonly SQLiteConnection _connection = new(ConnectionString);
    private readonly Lock _databaseLock = new();

    private bool _disposed;

    private HistoryManager()
    {
        Initialize();
    }

    private void Initialize()
    {
        _connection.Open();

        const string createTableQuery = """
                                        CREATE TABLE IF NOT EXISTS history (
                                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                                            DirectoryName TEXT NOT NULL,
                                            Url TEXT NOT NULL,
                                            Date DATETIME NOT NULL,
                                            NumUrls INTEGER NOT NULL
                                        );
                                        """;
        const string createIndexQuery = "CREATE INDEX IF NOT EXISTS url_index ON history (Url);";

        using var createTableCmd = new SQLiteCommand(createTableQuery, _connection);
        createTableCmd.ExecuteNonQuery();
        using var createIndexCmd = new SQLiteCommand(createIndexQuery, _connection);
        createIndexCmd.ExecuteNonQuery();
    }

    /// <summary>Inserts a new history record.</summary>
    /// <param name="entry">The entry to insert.</param>
    /// <param name="transaction">An existing transaction to insert within, or null to run standalone.</param>
    public void InsertHistoryRecord(HistoryEntry entry, SQLiteTransaction? transaction = null)
    {
        lock (_databaseLock)
        {
            const string insertQuery = """
                                       INSERT INTO history (DirectoryName, Url, Date, NumUrls)
                                       VALUES (@DirectoryName, @Url, @Date, @NumUrls);
                                       """;

            using var insertCmd = transaction is null
                ? new SQLiteCommand(insertQuery, _connection)
                : new SQLiteCommand(insertQuery, _connection, transaction);

            insertCmd.Parameters.AddWithValue("@DirectoryName", entry.DirectoryName);
            insertCmd.Parameters.AddWithValue("@Url", entry.Url);
            insertCmd.Parameters.AddWithValue("@Date",
                entry.Date.ToSqliteString()); // Format date to SQLite DATETIME format
            insertCmd.Parameters.AddWithValue("@NumUrls", entry.NumUrls);

            insertCmd.ExecuteNonQuery();
        }
    }

    /// <summary>Updates the recorded date for the history entry matching the given URL.</summary>
    /// <param name="url">URL of the entry to update.</param>
    /// <param name="date">New date to set.</param>
    /// <param name="transaction">An existing transaction to update within, or null to run standalone.</param>
    public void UpdateDateByUrl(string url, DateTime date, SQLiteTransaction? transaction = null)
    {
        lock (_databaseLock)
        {
            const string updateQuery = """
                                       UPDATE history
                                       SET Date = @Date
                                       WHERE Url = @Url;
                                       """;

            using var updateCmd = transaction is null
                ? new SQLiteCommand(updateQuery, _connection)
                : new SQLiteCommand(updateQuery, _connection, transaction);
            updateCmd.Parameters.AddWithValue("@Date", date.ToSqliteString());
            updateCmd.Parameters.AddWithValue("@Url", url);

            updateCmd.ExecuteNonQuery();
        }
    }

    /// <summary>Looks up the URL for a given history record id.</summary>
    /// <param name="id">The history record's id.</param>
    /// <param name="transaction">An existing transaction to query within, or null to run standalone.</param>
    /// <returns>The record's URL, or null if no record with that id exists.</returns>
    public string? GetUrlById(int id, SQLiteTransaction? transaction = null)
    {
        lock (_databaseLock)
        {
            const string selectQuery = """
                                       SELECT Url FROM history
                                       WHERE id = @Id;
                                       """;

            using var selectCmd = transaction is null
                ? new SQLiteCommand(selectQuery, _connection)
                : new SQLiteCommand(selectQuery, _connection, transaction);
            selectCmd.Parameters.AddWithValue("@Id", id);

            return (string?)selectCmd.ExecuteScalar();
        }
    }

    /// <summary>Updates the URL for a given history record id.</summary>
    /// <param name="id">The history record's id.</param>
    /// <param name="url">The new URL to set.</param>
    /// <param name="transaction">An existing transaction to update within, or null to run standalone.</param>
    public void UpdateHistoryEntryUrlById(int id, string url, SQLiteTransaction? transaction = null)
    {
        lock (_databaseLock)
        {
            const string updateQuery = """
                                       UPDATE history
                                       SET Url = @Url
                                       WHERE id = @Id;
                                       """;

            using var updateCmd = transaction is null
                ? new SQLiteCommand(updateQuery, _connection)
                : new SQLiteCommand(updateQuery, _connection, transaction);
            updateCmd.Parameters.AddWithValue("@Url", url);
            updateCmd.Parameters.AddWithValue("@Id", id);

            updateCmd.ExecuteNonQuery();
        }
    }

    /// <summary>Gets the total number of history records.</summary>
    /// <returns>The total record count.</returns>
    public int GetHistoryEntryCount()
    {
        lock (_databaseLock)
        {
            const string selectQuery = "SELECT COUNT(*) FROM history;";

            using var selectCmd = new SQLiteCommand(selectQuery, _connection);

            return Convert.ToInt32(selectCmd.ExecuteScalar());
        }
    }

    /// <summary>Checks whether a URL already has a history record.</summary>
    /// <param name="url">URL to check.</param>
    /// <returns>True if at least one record exists for this URL.</returns>
    public bool UrlInHistory(string url)
    {
        lock (_databaseLock)
        {
            const string selectQuery = """
                                       SELECT COUNT(*) FROM history
                                       WHERE Url = @Url;
                                       """;

            using var selectCmd = new SQLiteCommand(selectQuery, _connection);
            selectCmd.Parameters.AddWithValue("@Url", url);

            return Convert.ToInt64(selectCmd.ExecuteScalar()) > 0;
        }
    }

    /// <summary>Gets the history record for a given URL.</summary>
    /// <param name="url">URL to look up.</param>
    /// <returns>The matching entry, or null if none exists.</returns>
    public HistoryEntry? GetHistoryByUrl(string url)
    {
        lock (_databaseLock)
        {
            const string selectQuery = """
                                       SELECT * FROM history
                                       WHERE Url = @Url;
                                       """;

            using var selectCmd = new SQLiteCommand(selectQuery, _connection);
            selectCmd.Parameters.AddWithValue("@Url", url);
            using var reader = selectCmd.ExecuteReader();

            return !reader.Read() ? null : ExtractHistoryEntry(reader);
        }
    }

    /// <summary>Gets the history record for a given directory name.</summary>
    /// <param name="directoryName">Directory name to look up.</param>
    /// <returns>The matching entry, or null if none exists.</returns>
    public HistoryEntry? GetHistoryEntryByDirectoryName(string directoryName)
    {
        lock (_databaseLock)
        {
            const string selectQuery = """
                                       SELECT * FROM history
                                       WHERE DirectoryName = @DirectoryName;
                                       """;

            using var selectCmd = new SQLiteCommand(selectQuery, _connection);
            selectCmd.Parameters.AddWithValue("@DirectoryName", directoryName);
            using var reader = selectCmd.ExecuteReader();

            return !reader.Read() ? null : ExtractHistoryEntry(reader);
        }
    }

    /// <summary>Gets every history record, ordered by date ascending.</summary>
    /// <returns>All history entries.</returns>
    public List<HistoryEntry> GetHistory()
    {
        lock (_databaseLock)
        {
            const string selectQuery = """
                                       SELECT * FROM history
                                       ORDER BY Date;
                                       """;

            using var selectCmd = new SQLiteCommand(selectQuery, _connection);
            using var reader = selectCmd.ExecuteReader();

            var history = new List<HistoryEntry>();
            while (reader.Read())
            {
                history.Add(ExtractHistoryEntry(reader));
            }

            return history;
        }
    }

    /// <summary>Gets a page of history records, optionally filtered by name, URL, or date.</summary>
    /// <param name="page">Zero-based page number.</param>
    /// <param name="offset">Number of records per page.</param>
    /// <param name="filter">Optional filter to apply; ignored if null or invalid (see <see cref="InvalidFilter"/>).</param>
    /// <returns>The matching page of entries, ordered by date descending.</returns>
    /// <exception cref="ArgumentException">The filter's runtime type is not one of the known <see cref="HistoryFilter"/> subtypes.</exception>
    public List<HistoryEntry> GetHistory(int page, int offset, HistoryFilter? filter = null)
    {
        if (filter is null || InvalidFilter(filter))
        {
            return ExecutePagedQuery("", _ => { }, page, offset);
        }

        return filter switch
        {
            HistoryNameFilter nameFilter => ExecutePagedQuery(
                "WHERE DirectoryName LIKE @DirectoryName",
                cmd => cmd.Parameters.AddWithValue("@DirectoryName", $"%{nameFilter.Name}%"),
                page, offset),
            HistoryUrlFilter urlFilter => ExecutePagedQuery(
                "WHERE Url LIKE @Url",
                cmd => cmd.Parameters.AddWithValue("@Url", $"%{urlFilter.Url}%"),
                page, offset),
            HistoryDateFilter dateFilter => GetHistoryByDateFilter(dateFilter, page, offset),
            _ => throw new ArgumentException("Unsupported filter type.", nameof(filter))
        };
    }

    private static bool InvalidFilter(HistoryFilter? filter)
    {
        if (filter is null)
        {
            return true;
        }

        return filter switch
        {
            HistoryNameFilter nameFilter => string.IsNullOrWhiteSpace(nameFilter.Name) ||
                                            nameFilter.Name.Length <= 5, // requires more than "name:"
            HistoryUrlFilter urlFilter => string.IsNullOrWhiteSpace(urlFilter.Url) ||
                                          urlFilter.Url.Length <= 4, // requires more than "url:"
            HistoryDateFilter dateFilter => dateFilter.Date == default,
            _ => true
        };
    }

    /// <summary>
    ///     Shared implementation for every paged history query: builds and runs
    ///     <c>SELECT * FROM history [whereClause] ORDER BY Date DESC LIMIT @Offset OFFSET @Page</c>,
    ///     letting the caller supply the WHERE clause and bind its own parameters.
    /// </summary>
    /// <param name="whereClause">A full WHERE clause (e.g. "WHERE Url LIKE @Url"), or "" for no filter.</param>
    /// <param name="bindParams">Callback to bind any parameters referenced by <paramref name="whereClause"/>.</param>
    /// <param name="page">Zero-based page number.</param>
    /// <param name="offset">Number of records per page.</param>
    /// <returns>The matching page of entries.</returns>
    private List<HistoryEntry> ExecutePagedQuery(string whereClause, Action<SQLiteCommand> bindParams,
                                                 int page, int offset)
    {
        lock (_databaseLock)
        {
            var pageId = page * offset;
            var query = $"""
                         SELECT * FROM history
                         {whereClause}
                         ORDER BY Date DESC
                         LIMIT @Offset OFFSET @Page;
                         """;

            Logger.Debug("Selecting {Limit} from offset {Offset} for page {Page}", offset, pageId, page);
            using var cmd = new SQLiteCommand(query, _connection);
            bindParams(cmd);
            cmd.Parameters.AddWithValue("@Offset", offset);
            cmd.Parameters.AddWithValue("@Page", pageId);
            using var reader = cmd.ExecuteReader();

            var history = new List<HistoryEntry>();
            while (reader.Read())
            {
                history.Add(ExtractHistoryEntry(reader));
            }

            return history;
        }
    }

    private List<HistoryEntry> GetHistoryByDateFilter(HistoryDateFilter dateFilter, int page, int offset)
    {
        var startDate = DateTime.MinValue;
        var endDate = DateTime.MaxValue;
        switch (dateFilter.FilterType)
        {
            case HistoryFilterType.DateStart:
                startDate = dateFilter.Date;
                break;
            case HistoryFilterType.DateEnd:
                endDate = dateFilter.Date;
                break;
            case HistoryFilterType.Url:
            case HistoryFilterType.DirectoryName:
            default:
                throw new InvalidOperationException();
        }

        return ExecutePagedQuery(
            "WHERE Date BETWEEN @StartDate AND @EndDate",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@StartDate", startDate.ToSqliteString());
                cmd.Parameters.AddWithValue("@EndDate", endDate.ToSqliteString());
            },
            page, offset);
    }

    /// <summary>
    /// Begins a transaction on the shared connection. Callers are responsible for their own synchronization for the
    /// lifetime of the transaction, since it is not held under this manager's internal lock.
    /// </summary>
    /// <returns>A new transaction on the shared connection.</returns>
    public SQLiteTransaction BeginTransaction()
    {
        // ReSharper disable once InconsistentlySynchronizedField
        return _connection.BeginTransaction();
    }

    /// <summary>
    ///     Imports history records from another NicheImageRipper database file, skipping any record whose
    ///     URL already exists in this history (dedup key: <see cref="HistoryEntry.Url"/>).
    /// </summary>
    /// <param name="filepath">Path to the external SQLite database to merge from.</param>
    public void MergeHistory(string filepath)
    {
        lock (_databaseLock)
        {
            var externalHistory = $"Data Source={filepath}";
            using var externalConnection = new SQLiteConnection(externalHistory);
            externalConnection.Open();

            const string selectQuery = "SELECT * FROM history;";
            using var selectCmd = new SQLiteCommand(selectQuery, externalConnection);
            using var reader = selectCmd.ExecuteReader();

            using var transaction = _connection.BeginTransaction();
            try
            {
                var inserted = 0;
                var updated = 0;
                while (reader.Read())
                {
                    var entry = ExtractHistoryEntry(reader);
                    var existing = GetHistoryByUrl(entry.Url);

                    if (existing is null)
                    {
                        InsertHistoryRecord(entry, transaction);
                        inserted++;
                    }
                    else if (entry.Date > existing.Date)
                    {
                        UpdateDateByUrl(entry.Url, entry.Date, transaction);
                        updated++;
                    }
                }

                transaction.Commit();
                Logger.Information("Merged history from {Filepath}: {Inserted} inserted, {Updated} updated",
                    filepath, inserted, updated);
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    /// <summary>Reads a <see cref="HistoryEntry"/> from the current row of a `SELECT * FROM history` reader.</summary>
    /// <param name="reader">Reader positioned on a history row.</param>
    /// <returns>The extracted entry.</returns>
    private static HistoryEntry ExtractHistoryEntry(SQLiteDataReader reader)
    {
        return new HistoryEntry
        {
            DirectoryName = reader.GetString(1),
            Url = reader.GetString(2),
            Date = DateTime.Parse(reader.GetString(3)),
            NumUrls = reader.GetInt32(4)
        };
    }

    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    private void ReleaseUnmanagedResources()
    {
        if (_connection.State == ConnectionState.Open)
        {
            _connection.Close();
        }

        _connection.Dispose();
    }

    /// <summary>Closes and disposes the underlying database connection. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~HistoryManager()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseUnmanagedResources();
    }
}