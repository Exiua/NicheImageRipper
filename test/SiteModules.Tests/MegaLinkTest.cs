using NicheImageRipper.SiteModules.Modules.Mega;

namespace SiteModules.Tests;

public class MegaLinkTest
{
    public static TheoryData<string, MegaLinkKind, string, string?> ValidLinks =>
        new()
        {
            {
                "https://mega.nz/file/ti9gkSab#daz8ahh0y0DcTrHIRKpxxxabxjFWH_lklU9scNaVvb8",
                MegaLinkKind.File, "https://mega.nz/file/ti9gkSab#daz8ahh0y0DcTrHIRKpxxxabxjFWH_lklU9scNaVvb8", null
            },
            {
                "https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbw",
                MegaLinkKind.Folder, "https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbw", null
            },
            {
                "https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbw/folder/AbCdEfGh",
                MegaLinkKind.Folder, "https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbw", "AbCdEfGh"
            },
            {
                "https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbw/file/Xy-_1234",
                MegaLinkKind.FolderFile, "https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbw", "Xy-_1234"
            },
            {
                "https://mega.nz/#!ti9gkSab!daz8ahh0y0DcTrHIRKpxxxabxjFWH_lklU9scNaVvb8",
                MegaLinkKind.File, "https://mega.nz/file/ti9gkSab#daz8ahh0y0DcTrHIRKpxxxabxjFWH_lklU9scNaVvb8", null
            },
            {
                "https://mega.nz/#F!s7dmnSIS!RmFL5zxGBHwsUjHJgEocbw",
                MegaLinkKind.Folder, "https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbw", null
            },
            {
                "https://mega.co.nz/#F!s7dmnSIS!RmFL5zxGBHwsUjHJgEocbw",
                MegaLinkKind.Folder, "https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbw", null
            },
        };

    [Theory]
    [MemberData(nameof(ValidLinks))]
    public void ParsesLinks(string url, MegaLinkKind kind, string baseUrl, string? childHandle)
    {
        Assert.True(MegaLink.TryParse(url, out var link));
        Assert.Equal(kind, link.Kind);
        Assert.Equal(baseUrl, link.BaseUrl);
        Assert.Equal(childHandle, link.ChildHandle);
    }

    [Theory]
    [InlineData("https://mega.nz/")]
    [InlineData("https://mega.nz/pro")]
    [InlineData("https://mega.nz/folder/s7dmnSIS")]
    [InlineData("https://example.com/file/abc#def")]
    public void RejectsNonLinks(string url)
    {
        Assert.False(MegaLink.TryParse(url, out _));
        Assert.False(new MegaLinkRule().Matches(url));
    }

    [Fact]
    public void FolderFileUrlRoundTrips()
    {
        Assert.True(MegaLink.TryParse("https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbw", out var folder));

        var url = folder.FolderFileUrl("Xy-_1234");

        Assert.Equal("https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbw/file/Xy-_1234", url);
        Assert.True(MegaLink.TryParse(url, out var file));
        Assert.Equal(MegaLinkKind.FolderFile, file.Kind);
        Assert.Equal("Xy-_1234", file.ChildHandle);
        Assert.Equal(url, file.ToString());
    }

    [Fact]
    public void FileLinksHaveNoFolderFiles()
    {
        Assert.True(MegaLink.TryParse("https://mega.nz/file/ti9gkSab#daz8ahh0y0DcTrHIRKpx", out var file));
        Assert.Throws<InvalidOperationException>(() => file.FolderFileUrl("Xy-_1234"));
    }
}
