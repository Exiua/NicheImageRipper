using System.Data;
using System.Data.SQLite;
using Dapper;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.SiteParsing;
using Serilog;

namespace NicheImageRipper.Core.PartialSaves;

public class PartialSaveManager
{
    private const int MaxSaves = 10;

    private static readonly string ConnectionString = new SQLiteConnectionStringBuilder
    {
        DataSource = "NicheImageRipper.db",
        ForeignKeys = true,
    }.ToString();

    private static readonly ILogger Logger = Log.ForContext<PartialSaveManager>();

    public static PartialSaveManager Instance { get; } = new();

    private readonly SQLiteConnection _connection = new(ConnectionString);

    private readonly Lock _databaseLock = new();

    private PartialSaveManager()
    {
        Initialize();
    }

    private void Initialize()
    {
        _connection.Open();

        const string createMainTableQuery = """
                                            CREATE TABLE IF NOT EXISTS partial_saves (
                                                -- Partial Save Metadata
                                                id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                Url TEXT NOT NULL,
                                                LastUsed DATETIME NOT NULL,
                                                -- PartialSaveEntry data
                                                Cookies TEXT NOT NULL,
                                                Referer TEXT NOT NULL,
                                                FilenameScheme INTEGER NOT NULL,
                                                MustGenerateManually BOOLEAN NOT NULL,
                                                NumUrls INTEGER NOT NULL,
                                                DirectoryName TEXT NOT NULL
                                            );
                                            """;
        const string createIndexQuery = "CREATE INDEX IF NOT EXISTS url_index ON partial_saves (Url);";

        // Subtable corresponds to the List<ImageLink> Urls property of PartialSaveEntry
        const string createSubTableQuery = """
                                           CREATE TABLE IF NOT EXISTS partial_save_urls (
                                               id INTEGER PRIMARY KEY AUTOINCREMENT,
                                               PartialSaveId INTEGER NOT NULL,
                                               Referer TEXT NOT NULL,
                                               LinkInfo INTEGER NOT NULL,
                                               Url TEXT NOT NULL,
                                               Filename TEXT,
                                               FOREIGN KEY (PartialSaveId) REFERENCES partial_saves(id) ON DELETE CASCADE
                                           );
                                           """;

        string[] queries = [createMainTableQuery, createSubTableQuery, createIndexQuery];
        foreach (var query in queries)
        {
            using var command = new SQLiteCommand(query, _connection);
            command.ExecuteNonQuery();
        }
    }

    public void AddPartialSave(string url, PartialSaveEntry partialSave)
    {
        var ripInfo = partialSave.RipInfo;

        lock (_databaseLock)
        {
            var openedConnection = false;

            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
                openedConnection = true;
            }

            using var transaction = _connection.BeginTransaction();

            try
            {
                const string findExistingQuery = """
                                                 SELECT id
                                                 FROM partial_saves
                                                 WHERE Url = @Url
                                                 LIMIT 1;
                                                 """;

                var existingId = _connection.QuerySingleOrDefault<long?>(
                    findExistingQuery,
                    new
                    {
                        Url = url
                    },
                    transaction);

                if (existingId.HasValue)
                {
                    DeletePartialSave(existingId.Value, transaction);
                }
                else
                {
                    const string countQuery = """
                                              SELECT COUNT(*)
                                              FROM partial_saves;
                                              """;

                    var saveCount = _connection.ExecuteScalar<int>(
                        countQuery,
                        transaction: transaction);

                    if (saveCount >= MaxSaves)
                    {
                        // LIFO by LastUsed: remove the most recently used save.
                        const string findSaveToEvictQuery = """
                                                            SELECT id
                                                            FROM partial_saves
                                                            ORDER BY LastUsed DESC, id DESC
                                                            LIMIT 1;
                                                            """;

                        var saveToEvictId = _connection.QuerySingle<long>(
                            findSaveToEvictQuery,
                            transaction: transaction);

                        DeletePartialSave(saveToEvictId, transaction);
                    }
                }

                const string insertPartialSaveQuery = """
                                                      INSERT INTO partial_saves
                                                      (
                                                          Url,
                                                          LastUsed,
                                                          Cookies,
                                                          Referer,
                                                          FilenameScheme,
                                                          MustGenerateManually,
                                                          NumUrls,
                                                          DirectoryName
                                                      )
                                                      VALUES
                                                      (
                                                          @Url,
                                                          @LastUsed,
                                                          @Cookies,
                                                          @Referer,
                                                          @FilenameScheme,
                                                          @MustGenerateManually,
                                                          @NumUrls,
                                                          @DirectoryName
                                                      );

                                                      SELECT last_insert_rowid();
                                                      """;

                var partialSaveId = _connection.ExecuteScalar<long>(
                    insertPartialSaveQuery,
                    new
                    {
                        Url = url,
                        LastUsed = DateTime.UtcNow,
                        partialSave.Cookies,
                        partialSave.Referer,
                        FilenameScheme = (int)ripInfo.FilenameScheme,
                        ripInfo.MustGenerateManually,
                        ripInfo.NumUrls,
                        ripInfo.DirectoryName
                    },
                    transaction);

                const string insertPartialSaveUrlQuery = """
                                                         INSERT INTO partial_save_urls
                                                         (
                                                             PartialSaveId,
                                                             Referer,
                                                             LinkInfo,
                                                             Url,
                                                             Filename
                                                         )
                                                         VALUES
                                                         (
                                                             @PartialSaveId,
                                                             @Referer,
                                                             @LinkInfo,
                                                             @Url,
                                                             @Filename
                                                         );
                                                         """;

                var urlRows = ripInfo.Urls.Select(imageLink =>
                {
                    if (string.IsNullOrWhiteSpace(imageLink.Url))
                    {
                        throw new ArgumentException(
                            "A partial-save URL entry cannot have an empty URL.",
                            nameof(partialSave));
                    }

                    return new
                    {
                        PartialSaveId = partialSaveId,
                        Referer = imageLink.Referer ?? string.Empty,
                        LinkInfo = (int)imageLink.LinkInfo,
                        imageLink.Url,
                        Filename = imageLink.Filename
                    };
                });

                _connection.Execute(
                    insertPartialSaveUrlQuery,
                    urlRows,
                    transaction);

                transaction.Commit();

                Logger.Information(
                    "Added or replaced partial save for {Url} with NumUrls {NumUrls}",
                    url,
                    ripInfo.NumUrls);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                if (openedConnection)
                {
                    _connection.Close();
                }
            }
        }
    }

    private void DeletePartialSave(
        long partialSaveId,
        SQLiteTransaction transaction)
    {
        // Explicitly delete the child rows so this still works if SQLite foreign
        // key enforcement was not enabled for this connection.
        const string deleteUrlsQuery = """
                                       DELETE FROM partial_save_urls
                                       WHERE PartialSaveId = @PartialSaveId;
                                       """;

        const string deleteSaveQuery = """
                                       DELETE FROM partial_saves
                                       WHERE id = @PartialSaveId;
                                       """;

        var parameters = new
        {
            PartialSaveId = partialSaveId
        };

        _connection.Execute(
            deleteUrlsQuery,
            parameters,
            transaction);

        _connection.Execute(
            deleteSaveQuery,
            parameters,
            transaction);
    }

    public PartialSaveEntry? GetPartialSave(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        const string getPartialSaveQuery = """
                                           SELECT
                                               id,
                                               Cookies,
                                               Referer,
                                               FilenameScheme,
                                               MustGenerateManually,
                                               NumUrls,
                                               DirectoryName
                                           FROM partial_saves
                                           WHERE Url = @Url
                                           LIMIT 1;
                                           """;

        const string getUrlsQuery = """
                                    SELECT
                                        Referer,
                                        LinkInfo,
                                        Url,
                                        Filename
                                    FROM partial_save_urls
                                    WHERE PartialSaveId = @PartialSaveId
                                    ORDER BY id;
                                    """;

        var openedConnection = false;

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
            openedConnection = true;
        }

        try
        {
            var row = _connection.QuerySingleOrDefault(
                getPartialSaveQuery,
                new
                {
                    Url = url
                });

            if (row == null)
            {
                return null;
            }

            var imageLinks = _connection.Query<ImageLink>(
                                             getUrlsQuery,
                                             new
                                             {
                                                 PartialSaveId = (long)row.id
                                             })
                                        .ToList();

            return new PartialSaveEntry
            {
                Cookies = row.Cookies,
                Referer = row.Referer,
                RipInfo = new RipInfo
                {
                    FilenameScheme = (FilenameScheme)(int)row.FilenameScheme,
                    MustGenerateManually = row.MustGenerateManually,
                    NumUrls = (int)row.NumUrls,
                    DirectoryName = row.DirectoryName,
                    Urls = imageLinks
                }
            };
        }
        finally
        {
            if (openedConnection)
            {
                _connection.Close();
            }
        }
    }

    public void ClearPartialSaves()
    {
        var openedConnection = false;

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
            openedConnection = true;
        }

        using var transaction = _connection.BeginTransaction();

        try
        {
            // We intend to delete all data with these two statements
            _connection.Execute(
                """
                -- noinspection SqlWithoutWhereForFile
                DELETE FROM partial_save_urls;
                """,
                transaction: transaction);

            _connection.Execute(
                """
                -- noinspection SqlWithoutWhereForFile
                DELETE FROM partial_saves;
                """,
                transaction: transaction);

            transaction.Commit();

            Logger.Information("Cleared all partial saves.");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            if (openedConnection)
            {
                _connection.Close();
            }
        }
    }
}