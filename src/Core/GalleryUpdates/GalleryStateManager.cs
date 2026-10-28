using System.Data.SQLite;
using System.Text.Json;
using Dapper;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Core.GalleryUpdates;

/// <summary>What earlier rips of an updatable gallery left behind.</summary>
/// <param name="Items">Items ripped so far, in gallery order.</param>
/// <param name="PendingUpdate">The plan of an update rip that hasn't finished yet, if any.</param>
public sealed record GalleryState(string Url, string DirectoryName, string? Cursor, List<GalleryItem> Items,
                                  GalleryUpdatePlan? PendingUpdate);

/// <summary>
///     Persists, per gallery URL, the items an <see cref="Sdk.SiteParsing.IUpdatableHtmlParser"/> rip downloaded and
///     the files they were saved as, so a later rip can download only what changed. Existing items are saved as soon
///     as their files are renamed, new ones only once the rip succeeds, so the stored state always matches the disk.
/// </summary>
public sealed class GalleryStateManager
{
    private static readonly string ConnectionString = new SQLiteConnectionStringBuilder
    {
        DataSource = AppData.GetPathForWrite("NicheImageRipper.db"),
        ForeignKeys = true,
    }.ToString();

    /// <summary>The shared singleton instance.</summary>
    public static GalleryStateManager Instance { get; } = new();

    private readonly SQLiteConnection _connection = new(ConnectionString);
    private readonly Lock _databaseLock = new();

    private GalleryStateManager()
    {
        _connection.Open();

        const string createStateTableQuery = """
                                             CREATE TABLE IF NOT EXISTS gallery_state (
                                                 id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                 Url TEXT NOT NULL UNIQUE,
                                                 DirectoryName TEXT NOT NULL,
                                                 Cursor TEXT,
                                                 PendingUpdate TEXT
                                             );
                                             """;
        const string createItemsTableQuery = """
                                             CREATE TABLE IF NOT EXISTS gallery_items (
                                                 id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                 GalleryId INTEGER NOT NULL,
                                                 ItemKey TEXT NOT NULL,
                                                 Version TEXT,
                                                 Position INTEGER NOT NULL,
                                                 Filename TEXT NOT NULL,
                                                 UNIQUE (GalleryId, ItemKey),
                                                 FOREIGN KEY (GalleryId) REFERENCES gallery_state(id) ON DELETE CASCADE
                                             );
                                             """;

        foreach (var query in new[] { createStateTableQuery, createItemsTableQuery })
        {
            using var cmd = new SQLiteCommand(query, _connection);
            cmd.ExecuteNonQuery();
        }
    }

    private sealed record StateRow(long Id, string Url, string DirectoryName, string? Cursor, string? PendingUpdate);

    private sealed record ItemRow(string ItemKey, string? Version, long Position, string Filename);

    public GalleryState? GetState(string url)
    {
        lock (_databaseLock)
        {
            var row = _connection.QuerySingleOrDefault<StateRow>(
                "SELECT id AS Id, Url, DirectoryName, Cursor, PendingUpdate FROM gallery_state WHERE Url = @url;",
                new { url });
            if (row is null)
            {
                return null;
            }

            var items = _connection.Query<ItemRow>(
                                       """
                                       SELECT ItemKey, Version, Position, Filename FROM gallery_items
                                       WHERE GalleryId = @Id ORDER BY Position;
                                       """, new { row.Id })
                                   .Select(i => new GalleryItem(i.ItemKey, i.Version, (int)i.Position, i.Filename))
                                   .ToList();
            var pending = row.PendingUpdate is null
                ? null
                : JsonSerializer.Deserialize<GalleryUpdatePlan>(row.PendingUpdate);
            return new GalleryState(row.Url, row.DirectoryName, row.Cursor, items, pending);
        }
    }

    /// <summary>Records <paramref name="plan"/> as the gallery's unfinished update, replacing any earlier one.</summary>
    public void SavePendingUpdate(string url, GalleryUpdatePlan plan)
    {
        lock (_databaseLock)
        {
            _connection.Execute("""
                                INSERT INTO gallery_state (Url, DirectoryName, PendingUpdate)
                                VALUES (@url, @DirectoryName, @PendingUpdate)
                                ON CONFLICT (Url) DO UPDATE SET
                                    DirectoryName = excluded.DirectoryName,
                                    PendingUpdate = excluded.PendingUpdate;
                                """,
                new { url, plan.DirectoryName, PendingUpdate = JsonSerializer.Serialize(plan) });
        }
    }

    /// <summary>
    ///     Called once <paramref name="plan"/>'s renames are done: replaces the stored items with
    ///     <see cref="GalleryUpdatePlan.ExistingItems"/> and marks the renames as applied.
    /// </summary>
    public void SaveRenamedItems(string url, GalleryUpdatePlan plan)
    {
        lock (_databaseLock)
        {
            using var transaction = _connection.BeginTransaction();
            plan.RenamesApplied = true;
            _connection.Execute("UPDATE gallery_state SET PendingUpdate = @PendingUpdate WHERE Url = @url;",
                new { url, PendingUpdate = JsonSerializer.Serialize(plan) }, transaction);
            var galleryId = GetGalleryId(url, transaction);
            _connection.Execute("DELETE FROM gallery_items WHERE GalleryId = @galleryId;", new { galleryId },
                transaction);
            InsertItems(galleryId, plan.ExistingItems, transaction);
            transaction.Commit();
        }
    }

    /// <summary>
    ///     Finishes an update: adds (or updates) the items that downloaded, saves the cursor (keeping the previous one
    ///     when the parser didn't set one) and clears the pending update.
    /// </summary>
    public void CompleteUpdate(string url, string? cursor, IReadOnlyList<GalleryItem> downloadedItems)
    {
        lock (_databaseLock)
        {
            using var transaction = _connection.BeginTransaction();
            var galleryId = GetGalleryId(url, transaction);
            InsertItems(galleryId, downloadedItems, transaction);
            _connection.Execute("""
                                UPDATE gallery_state SET Cursor = COALESCE(@cursor, Cursor), PendingUpdate = NULL
                                WHERE id = @galleryId;
                                """, new { cursor, galleryId }, transaction);
            transaction.Commit();
        }
    }

    private long GetGalleryId(string url, SQLiteTransaction transaction) =>
        _connection.ExecuteScalar<long>("SELECT id FROM gallery_state WHERE Url = @url;", new { url }, transaction);

    private void InsertItems(long galleryId, IEnumerable<GalleryItem> items, SQLiteTransaction transaction)
    {
        _connection.Execute("""
                            INSERT INTO gallery_items (GalleryId, ItemKey, Version, Position, Filename)
                            VALUES (@galleryId, @Key, @Version, @Position, @Filename)
                            ON CONFLICT (GalleryId, ItemKey) DO UPDATE SET
                                Version = excluded.Version,
                                Position = excluded.Position,
                                Filename = excluded.Filename;
                            """,
            items.Select(i => new { galleryId, i.Key, i.Version, i.Position, i.Filename }), transaction);
    }
}
