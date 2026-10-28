using NicheImageRipper.Core.GalleryUpdates;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Core.Tests;

public class GalleryUpdatePlannerTest
{
    private static GalleryItem Stored(string key, int position, string ext = ".jpg", string? version = null) =>
        new(key, version, position, position + ext);

    private static ParsedGalleryItem Parsed(string key, string ext = ".jpg", string? version = null,
                                            FilenameScheme scheme = FilenameScheme.Chronological) =>
        new(key, version, FileLink.Create($"https://example.com/{key}{ext}", scheme));

    private static GalleryUpdatePlan Plan(IReadOnlyList<GalleryItem> stored, IReadOnlyList<ParsedGalleryItem> parsed,
                                          GalleryUpdateStrategy strategy,
                                          NewItemPlacement placement = NewItemPlacement.End,
                                          GalleryUpdateMode mode = GalleryUpdateMode.UpdatesOnly,
                                          FilenameScheme scheme = FilenameScheme.Chronological) =>
        GalleryUpdatePlanner.Plan(stored, parsed, strategy, placement, mode, scheme, "dir", "cursor");

    private static List<(string Key, int Position, string Filename)> Downloads(GalleryUpdatePlan plan) =>
        plan.PendingItems.Select(p => (p.Key, p.Position, plan.Downloads[p.DownloadIndex].Filename)).ToList();

    [Fact]
    public void FirstRipDownloadsEverythingInParsedOrder()
    {
        var plan = Plan([], [Parsed("a"), Parsed("b")], GalleryUpdateStrategy.NewItemsOnly);

        Assert.Equal([("a", 0, "0.jpg"), ("b", 1, "1.jpg")], Downloads(plan));
        Assert.Empty(plan.Renames);
        Assert.Empty(plan.ExistingItems);
    }

    [Fact]
    public void NewItemsOnlyAppendsNewItemsWithoutRenaming()
    {
        var plan = Plan([Stored("a", 0), Stored("b", 1)], [Parsed("a"), Parsed("b"), Parsed("c")],
            GalleryUpdateStrategy.NewItemsOnly);

        Assert.Equal([("c", 2, "2.jpg")], Downloads(plan));
        Assert.Empty(plan.Renames);
    }

    [Fact]
    public void NewItemsOnlyAtStartShiftsExistingFiles()
    {
        // A newest-first feed: the parser stopped at the first known item
        var plan = Plan([Stored("a", 0), Stored("b", 1, ".png")], [Parsed("c"), Parsed("d"), Parsed("a")],
            GalleryUpdateStrategy.NewItemsOnly, NewItemPlacement.Start);

        Assert.Equal([("c", 0, "0.jpg"), ("d", 1, "1.jpg")], Downloads(plan));
        Assert.Equal([new GalleryFileRename("0.jpg", "2.jpg"), new GalleryFileRename("1.png", "3.png")], plan.Renames);
        Assert.Equal([new GalleryItem("a", null, 2, "2.jpg"), new GalleryItem("b", null, 3, "3.png")],
            plan.ExistingItems);
    }

    [Fact]
    public void NewItemsOnlyNeverDownloadsExistingItemsAgain()
    {
        var plan = Plan([Stored("a", 0, version: "1")], [Parsed("a", version: "2")],
            GalleryUpdateStrategy.NewItemsOnly);

        Assert.True(plan.HasNothingToDownload);
    }

    [Fact]
    public void FullDiffInsertsNewItemsWhereTheyAppear()
    {
        var plan = Plan([Stored("a", 0), Stored("b", 1), Stored("c", 2)],
            [Parsed("a"), Parsed("x"), Parsed("b"), Parsed("c")], GalleryUpdateStrategy.FullDiff);

        Assert.Equal([("x", 1, "1.jpg")], Downloads(plan));
        Assert.Equal([new GalleryFileRename("1.jpg", "2.jpg"), new GalleryFileRename("2.jpg", "3.jpg")], plan.Renames);
    }

    [Fact]
    public void FullDiffKeepsRemovedItemsAfterTheirPreviousNeighbour()
    {
        var plan = Plan([Stored("a", 0), Stored("b", 1), Stored("c", 2)], [Parsed("a"), Parsed("c"), Parsed("d")],
            GalleryUpdateStrategy.FullDiff);

        Assert.Equal(["a", "b", "c"], plan.ExistingItems.Select(i => i.Key));
        Assert.Equal([("d", 3, "3.jpg")], Downloads(plan));
        Assert.Empty(plan.Renames);
    }

    [Fact]
    public void FullDiffKeepsARemovedFirstItemFirst()
    {
        var plan = Plan([Stored("a", 0), Stored("b", 1)], [Parsed("x"), Parsed("b")], GalleryUpdateStrategy.FullDiff);

        Assert.Equal([new GalleryItem("a", null, 0, "0.jpg"), new GalleryItem("b", null, 2, "2.jpg")],
            plan.ExistingItems);
        Assert.Equal([("x", 1, "1.jpg")], Downloads(plan));
    }

    [Fact]
    public void FullDiffReordersExistingItemsToMatchTheParse()
    {
        var plan = Plan([Stored("a", 0), Stored("b", 1)], [Parsed("b"), Parsed("a")], GalleryUpdateStrategy.FullDiff);

        Assert.True(plan.HasNothingToDownload);
        Assert.Equal([new GalleryFileRename("1.jpg", "0.jpg"), new GalleryFileRename("0.jpg", "1.jpg")], plan.Renames);
    }

    [Fact]
    public void FullDiffDownloadsChangedItemsAgainAndReplacesTheOldFile()
    {
        var plan = Plan([Stored("a", 0, ".png", version: "1")], [Parsed("a", ".jpg", version: "2")],
            GalleryUpdateStrategy.FullDiff);

        var pending = Assert.Single(plan.PendingItems);
        Assert.Equal("0.jpg", plan.Downloads[pending.DownloadIndex].Filename);
        Assert.Equal("2", pending.Version);
        Assert.Equal("0.png", pending.ReplacedFilename);
        // Stays at the old version until the new download succeeds
        Assert.Equal("1", Assert.Single(plan.ExistingItems).Version);
    }

    [Fact]
    public void FullDiffAdoptsAVersionForItemsStoredWithoutOne()
    {
        var plan = Plan([Stored("a", 0)], [Parsed("a", version: "1")], GalleryUpdateStrategy.FullDiff);

        Assert.True(plan.HasNothingToDownload);
        Assert.Equal("1", Assert.Single(plan.ExistingItems).Version);
    }

    [Fact]
    public void FullModeDownloadsEverythingAndForgetsEarlierItems()
    {
        var plan = Plan([Stored("a", 0), Stored("old", 1)], [Parsed("a"), Parsed("b")],
            GalleryUpdateStrategy.NewItemsOnly, mode: GalleryUpdateMode.Full);

        Assert.Equal([("a", 0, "0.jpg"), ("b", 1, "1.jpg")], Downloads(plan));
        Assert.Empty(plan.ExistingItems);
        Assert.Empty(plan.Renames);
    }

    [Fact]
    public void NonChronologicalNamesAreNeverRenamed()
    {
        var stored = new GalleryItem("a", null, 0, "a.jpg");
        var plan = Plan([stored], [Parsed("b", scheme: FilenameScheme.Original)], GalleryUpdateStrategy.NewItemsOnly,
            NewItemPlacement.Start, scheme: FilenameScheme.Original);

        Assert.Equal([("b", 0, "b.jpg")], Downloads(plan));
        Assert.Empty(plan.Renames);
        Assert.Equal(stored with { Position = 1 }, Assert.Single(plan.ExistingItems));
    }

    [Fact]
    public void DuplicateParsedKeysAreDownloadedOnce()
    {
        var plan = Plan([], [Parsed("a"), Parsed("a")], GalleryUpdateStrategy.NewItemsOnly);

        Assert.Single(plan.PendingItems);
    }

    [Fact]
    public void PlanCarriesDirectoryAndCursor()
    {
        var plan = Plan([], [Parsed("a")], GalleryUpdateStrategy.NewItemsOnly);

        Assert.Equal("dir", plan.DirectoryName);
        Assert.Equal("cursor", plan.Cursor);
    }
}
