using NicheImageRipper.Core.GalleryUpdates;

namespace NicheImageRipper.Core.Tests;

/// <summary>
///     Runs against a real database in a temp data folder. <see cref="GalleryStateManager"/> is a process-wide
///     singleton, so each test uses its own gallery URL.
/// </summary>
[Collection(DataFolderCollection.Name)]
public class GalleryStateManagerTest
{
    private static GalleryStateManager Galleries => GalleryStateManager.Instance;

    private static string UniqueUrl() => $"https://example.com/gallery-{Guid.NewGuid():N}";

    private static GalleryUpdatePlan Plan(List<GalleryItem> existing, List<PendingGalleryItem> pending,
                                          string? cursor = null) => new()
    {
        DirectoryName = "dir (Example)",
        Cursor = cursor,
        Renames = [new GalleryFileRename("0.jpg", "1.jpg")],
        ExistingItems = existing,
        PendingItems = pending,
    };

    [Fact]
    public void UnknownGalleryHasNoState()
    {
        Assert.Null(Galleries.GetState(UniqueUrl()));
    }

    [Fact]
    public void PendingUpdateRoundTrips()
    {
        var url = UniqueUrl();
        Galleries.SavePendingUpdate(url, Plan([], [new PendingGalleryItem("a", "1", 0, 0, null)], "c1"));

        var state = Galleries.GetState(url)!;
        Assert.Equal("dir (Example)", state.DirectoryName);
        Assert.Null(state.Cursor); // Only saved once the update completes
        Assert.Empty(state.Items);
        var pending = state.PendingUpdate!;
        Assert.False(pending.RenamesApplied);
        Assert.Equal("c1", pending.Cursor);
        Assert.Equal([new GalleryFileRename("0.jpg", "1.jpg")], pending.Renames);
        Assert.Equal([new PendingGalleryItem("a", "1", 0, 0, null)], pending.PendingItems);
    }

    [Fact]
    public void FullUpdateLifecycleStoresItemsInGalleryOrder()
    {
        var url = UniqueUrl();
        Galleries.SavePendingUpdate(url, Plan([], [], "c1"));
        Galleries.SaveRenamedItems(url, Plan([], [], "c1"));
        Galleries.CompleteUpdate(url, "c1", [new GalleryItem("b", null, 1, "1.jpg"), new GalleryItem("a", null, 0, "0.jpg")]);

        // Second update: an item inserted at the start moves both existing ones
        var plan = Plan([new GalleryItem("a", null, 1, "1.jpg"), new GalleryItem("b", null, 2, "2.jpg")],
            [new PendingGalleryItem("x", null, 0, 0, null)], "c2");
        Galleries.SavePendingUpdate(url, plan);
        Galleries.SaveRenamedItems(url, plan);

        var midway = Galleries.GetState(url)!;
        Assert.True(midway.PendingUpdate!.RenamesApplied);
        Assert.Equal(["a", "b"], midway.Items.Select(i => i.Key));
        Assert.Equal("c1", midway.Cursor);

        Galleries.CompleteUpdate(url, null, [new GalleryItem("x", null, 0, "0.jpg")]);

        var state = Galleries.GetState(url)!;
        Assert.Null(state.PendingUpdate);
        Assert.Equal("c1", state.Cursor); // A null cursor keeps the previous one
        Assert.Equal([
            new GalleryItem("x", null, 0, "0.jpg"),
            new GalleryItem("a", null, 1, "1.jpg"),
            new GalleryItem("b", null, 2, "2.jpg"),
        ], state.Items);
    }

    [Fact]
    public void CompletingWithAReDownloadedItemUpdatesItsVersion()
    {
        var url = UniqueUrl();
        var plan = Plan([new GalleryItem("a", "1", 0, "0.png")], [new PendingGalleryItem("a", "2", 0, 0, "0.png")]);
        Galleries.SavePendingUpdate(url, plan);
        Galleries.SaveRenamedItems(url, plan);
        Galleries.CompleteUpdate(url, "c", [new GalleryItem("a", "2", 0, "0.jpg")]);

        Assert.Equal(new GalleryItem("a", "2", 0, "0.jpg"), Assert.Single(Galleries.GetState(url)!.Items));
    }
}
