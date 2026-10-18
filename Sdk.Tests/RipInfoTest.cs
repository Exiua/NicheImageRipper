using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Sdk.Tests;

public class RipInfoTest
{
    [Fact]
    public async Task UrlsBecomeFileLinks()
    {
        var ripInfo = await RipInfo.FromUrlListAsync(["https://example.com/a.jpg", "https://example.com/b.png"],
            "Gallery", FilenameScheme.Original, referer: "https://example.com/");

        Assert.Equal(["https://example.com/a.jpg", "https://example.com/b.png"], ripInfo.Urls.Select(link => link.Url));
        Assert.Equal(2, ripInfo.NumUrls);
        Assert.Equal("Gallery", ripInfo.DirectoryName);
        Assert.Equal(DownloadMode.List, ripInfo.DownloadMode);
        Assert.All(ripInfo.Urls, link => Assert.Equal("https://example.com/", link.Referer));
    }

    [Fact]
    public async Task DuplicateUrlsAreDropped()
    {
        var ripInfo = await RipInfo.FromUrlListAsync(
            ["https://example.com/a.jpg", "https://example.com/b.jpg", "https://example.com/a.jpg"], "Gallery",
            FilenameScheme.Original);

        Assert.Equal(["https://example.com/a.jpg", "https://example.com/b.jpg"], ripInfo.Urls.Select(link => link.Url));
        Assert.Equal(2, ripInfo.NumUrls);
    }

    [Fact]
    public async Task FilenamesStayWithTheirUrlsWhenDuplicatesAreDropped()
    {
        var ripInfo = await RipInfo.FromUrlListWithFilenamesAsync(
            ["https://example.com/1", "https://example.com/1", "https://example.com/2", "https://example.com/3"],
            "Gallery", FilenameScheme.Original, ["first.jpg", "first-again.jpg", "second.jpg", "third.jpg"]);

        Assert.Equal(["https://example.com/1", "https://example.com/2", "https://example.com/3"],
            ripInfo.Urls.Select(link => link.Url));
        Assert.Equal(["first.jpg", "second.jpg", "third.jpg"], ripInfo.Urls.Select(link => link.Filename));
    }

    [Fact]
    public async Task NameReuseNumbersOriginalFilenames()
    {
        // Sites that reuse names within a gallery (e.g. every chapter starts at 001.jpg) need numbered names
        var ripInfo = await RipInfo.FromUrlListAsync(["https://example.com/1/001.jpg", "https://example.com/2/001.jpg"],
            "Gallery", FilenameScheme.Original, nameReuse: true);

        Assert.Equal(FilenameScheme.Chronological, ripInfo.FilenameScheme);
        Assert.Equal(2, ripInfo.Urls.Select(link => link.Filename).Distinct().Count());
    }

    [Fact]
    public async Task NameReuseKeepsAnExplicitScheme()
    {
        var ripInfo = await RipInfo.FromUrlListAsync(["https://example.com/a.jpg"], "Gallery", FilenameScheme.Hash,
            nameReuse: true);

        Assert.Equal(FilenameScheme.Hash, ripInfo.FilenameScheme);
    }

    [Fact]
    public async Task ExistingFileLinksAreRenumberedForChronologicalNames()
    {
        var links = new List<StringFileLinkWrapper>
        {
            FileLink.WithFilename("https://example.com/x", "cover.jpg", FilenameScheme.Original),
            FileLink.WithFilename("https://example.com/y", "page.png", FilenameScheme.Original),
        };

        var ripInfo = await RipInfo.FromUrlListAsync(links, "Gallery", FilenameScheme.Chronological);

        Assert.Equal(["0.jpg", "1.png"], ripInfo.Urls.Select(link => link.Filename));
    }

    [Fact]
    public async Task MaxConcurrentDownloadsIsKept()
    {
        var ripInfo = await RipInfo.FromUrlListAsync(["https://example.com/a.mp4"], "Videos", FilenameScheme.Original,
            maxConcurrentDownloads: 3);

        Assert.Equal(3, ripInfo.MaxConcurrentDownloads);
    }

    [Fact]
    public void GenerateInfoKeepsTheGivenCount()
    {
        var ripInfo = RipInfo.FromGenerateInfo("https://example.com/page/1", "Generated", 40);

        Assert.True(ripInfo.MustGenerateManually);
        Assert.Equal(40, ripInfo.NumUrls);
    }

    [Fact]
    public void LongDirectoryNamesAreShortened()
    {
        var ripInfo = RipInfo.Empty.WithDirectoryName(new string('a', 500));

        Assert.True(ripInfo.DirectoryName.Length <= 200);
    }

    [Fact]
    public void EmptyHasNoLinks()
    {
        Assert.Empty(RipInfo.Empty.Urls);
        Assert.Equal(0, RipInfo.Empty.NumUrls);
    }
}
