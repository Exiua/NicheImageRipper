using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.FileDownloading;
using NicheImageRipper.Sdk.Utility;
using NicheImageRipper.SiteModules.Modules.Mega;
using Serilog;

namespace SiteModules.Tests;

/// <summary>
///     Integration tests against MEGA through the native SDK. They're opt-in and skip unless set up:
///     <list type="bullet">
///         <item><c>NIR_MEGA_APP_KEY</c>: a MEGA app key (https://mega.nz/sdk).</item>
///         <item><c>NIR_MEGA_TEST_FILE_URL</c> and/or <c>NIR_MEGA_TEST_FOLDER_URL</c>: public links to test with.</item>
///         <item>megasdk.dll built into lib/MegaSdk (see its README).</item>
///     </list>
///     Downloads go to TestFiles in the app data folder.
/// </summary>
[Trait("Category", "Integration")]
public class MegaTest
{
    private const string AppKeyVariable = "NIR_MEGA_APP_KEY";
    private const string FileUrlVariable = "NIR_MEGA_TEST_FILE_URL";
    private const string FolderUrlVariable = "NIR_MEGA_TEST_FOLDER_URL";

    private readonly ITestOutputHelper _output;

    public MegaTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ExpandAndDownloadFileTest()
    {
        var fileUrl = RequireSetup(FileUrlVariable);

        var (links, nextIndex) = await MegaHelper.ExpandAsync(fileUrl, 0, FilenameScheme.Original,
            TestContext.Current.CancellationToken);

        var link = Assert.Single(links);
        Assert.Equal(1, nextIndex);
        Assert.Equal(MegaLinkInfo.Mega, link.LinkInfo);
        _output.WriteLine($"{link.Url}  {link.Filename}");

        var path = Path.Combine(AppData.EnsureDirectory("TestFiles", "MegaTestFile"), link.Filename);
        var result = await new MegaDownloadStrategy().DownloadAsync(link, path, CreateContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(DownloadOutcome.Success, result.Outcome);
        Assert.True(new FileInfo(path).Length > 0, $"Downloaded file is empty: {path}");
    }

    [Fact]
    public async Task ExpandAndDownloadFolderTest()
    {
        var folderUrl = RequireSetup(FolderUrlVariable);

        var (links, nextIndex) = await MegaHelper.ExpandAsync(folderUrl, 0, FilenameScheme.Original,
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(links);
        Assert.Equal(links.Count, nextIndex);
        Assert.All(links, link => Assert.Equal(MegaLinkInfo.Mega, link.LinkInfo));
        foreach (var link in links)
        {
            _output.WriteLine($"{link.Url}  {link.Filename}");
        }

        // Only the first file, to keep the test quick
        var first = links[0];
        var path = Path.Combine(AppData.EnsureDirectory("TestFiles", "MegaTestFolder"), first.Filename);
        var result = await new MegaDownloadStrategy().DownloadAsync(first, path, CreateContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(DownloadOutcome.Success, result.Outcome);
        Assert.True(new FileInfo(path).Length > 0, $"Downloaded file is empty: {path}");
    }

    /// <summary>Skips the test unless it's configured, and puts the app key in the mega site config.</summary>
    private static string RequireSetup(string urlVariable)
    {
        var appKey = Environment.GetEnvironmentVariable(AppKeyVariable);
        var url = Environment.GetEnvironmentVariable(urlVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(appKey), $"Set {AppKeyVariable} to run MEGA tests");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(url), $"Set {urlVariable} to run this MEGA test");

        var sites = Config.Instance.Sites;
        var siteConfig = sites.GetValueOrDefault(MegaParser.ParserName) ?? new SiteConfig();
        siteConfig.Key = appKey;
        sites[MegaParser.ParserName] = siteConfig;
        return url!;
    }

    /// <summary>
    ///     The Mega strategy only uses the logger; there's no browser behind <see cref="DownloadContext.WebDriver" />.
    /// </summary>
    private static DownloadContext CreateContext() => new()
    {
        RequestHeaders = new Dictionary<string, string>(),
        WebDriver = null!,
        Session = new HttpClient(),
        SiteName = "mega",
        SleepTime = 0,
        Logger = Log.Logger,
        HeaderModifiers = [],
        ErrorHandlers = [],
        PostDownloadValidators = [],
    };
}
