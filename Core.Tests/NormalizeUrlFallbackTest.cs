using Ripper = NicheImageRipper.Core.NicheImageRipper;

namespace NicheImageRipper.Core.Tests;

/// <summary>
///     Core on its own, with no site modules loaded. Site-specific normalization is covered in SiteModules.Tests.
/// </summary>
[Collection(DataFolderCollection.Name)]
public class NormalizeUrlFallbackTest
{
    [Theory]
    [InlineData("https://unknown.example/gallery/1?page=2&sort=new", "https://unknown.example/gallery/1")]
    [InlineData("https://unknown.example/gallery/1", "https://unknown.example/gallery/1")]
    public void UnknownSitesLoseTheirQuery(string url, string expected)
    {
        Assert.Equal(expected, Ripper.NormalizeUrl(url));
    }

    [Fact]
    public void UnfinishedRipsAreKeptInTheDataFolder()
    {
        Assert.Equal(Sdk.Utility.AppData.GetPath("UnfinishedRips"), Ripper.UnfinishedUrlsDirectory);
    }
}
