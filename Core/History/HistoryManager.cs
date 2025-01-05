﻿using System.Data.SQLite;
using Core.DataStructures;
using Core.ExtensionMethods;

namespace Core.History;

public class HistoryManager : IDisposable
{
    private const string ConnectionString = "Data Source=history.db";
    
    public static HistoryManager Instance { get; } = new();
    
    private readonly SQLiteConnection _connection = new(ConnectionString);
    
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
    
    public void InsertHistoryRecord(HistoryEntry entry)
    {
        const string insertQuery = """
                                   INSERT INTO history (DirectoryName, Url, Date, NumUrls)
                                   VALUES (@DirectoryName, @Url, @Date, @NumUrls);
                                   """;

        using var insertCmd = new SQLiteCommand(insertQuery, _connection);
        insertCmd.Parameters.AddWithValue("@DirectoryName", entry.DirectoryName);
        insertCmd.Parameters.AddWithValue("@Url", entry.Url);
        insertCmd.Parameters.AddWithValue("@Date", entry.Date.ToSqliteString()); // Format date to SQLite DATETIME format
        insertCmd.Parameters.AddWithValue("@NumUrls", entry.NumUrls);

        insertCmd.ExecuteNonQuery();
    }
    
    public void UpdateDateByUrl(string url, DateTime date)
    {
        const string updateQuery = """
                                   UPDATE history
                                   SET Date = @Date
                                   WHERE Url = @Url;
                                   """;

        using var updateCmd = new SQLiteCommand(updateQuery, _connection);
        updateCmd.Parameters.AddWithValue("@Date", date.ToSqliteString()); // Format date to SQLite DATETIME format
        updateCmd.Parameters.AddWithValue("@Url", url);

        updateCmd.ExecuteNonQuery();
    }
    
    public string? GetUrlById(int id)
    {
        const string selectQuery = """
                                   SELECT Url FROM history
                                   WHERE id = @Id;
                                   """;

        using var selectCmd = new SQLiteCommand(selectQuery, _connection);
        selectCmd.Parameters.AddWithValue("@Id", id);

        return (string?) selectCmd.ExecuteScalar();
    }
    
    public void UpdateHistoryEntryUrlById(int id, string url)
    {
        const string updateQuery = """
                                   UPDATE history
                                   SET Url = @Url
                                   WHERE id = @Id;
                                   """;

        using var updateCmd = new SQLiteCommand(updateQuery, _connection);
        updateCmd.Parameters.AddWithValue("@Url", url);
        updateCmd.Parameters.AddWithValue("@Id", id);

        updateCmd.ExecuteNonQuery();
    }
    
    public int GetHistoryEntryCount()
    {
        const string selectQuery = "SELECT COUNT(*) FROM history;";

        using var selectCmd = new SQLiteCommand(selectQuery, _connection);

        return Convert.ToInt32(selectCmd.ExecuteScalar());
    }
    
    public bool UrlInHistory(string url)
    {
        const string selectQuery = """
                                   SELECT COUNT(*) FROM history
                                   WHERE Url = @Url;
                                   """;

        using var selectCmd = new SQLiteCommand(selectQuery, _connection);
        selectCmd.Parameters.AddWithValue("@Url", url);

        return (long) selectCmd.ExecuteScalar() > 0;
    }

    public HistoryEntry? GetHistoryByUrl(string url)
    {
        const string selectQuery = """
                                   SELECT * FROM history
                                   WHERE Url = @Url;
                                   """;
        
        using var selectCmd = new SQLiteCommand(selectQuery, _connection);
        selectCmd.Parameters.AddWithValue("@Url", url);
        using var reader = selectCmd.ExecuteReader();
        
        if (!reader.Read())
        {
            return null;
        }
        
        return new HistoryEntry
        {
            DirectoryName = reader.GetString(1),
            Url = reader.GetString(2),
            Date = DateTime.Parse(reader.GetString(3)),
            NumUrls = reader.GetInt32(4)
        };
    }
    
    public List<HistoryEntry> GetHistory()
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
            var entry = new HistoryEntry
            {
                DirectoryName = reader.GetString(1),
                Url = reader.GetString(2),
                Date = DateTime.Parse(reader.GetString(3)),
                NumUrls = reader.GetInt32(4)
            };
            history.Add(entry);
        }

        return history;
    }

    private void ReleaseUnmanagedResources()
    {
        // Dispose of the SQLite connection
        if (_connection.State == System.Data.ConnectionState.Open)
        {
            _connection.Close();
        }
        _connection.Dispose();
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~HistoryManager()
    {
        ReleaseUnmanagedResources();
    }
}