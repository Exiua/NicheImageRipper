using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.FileDownloading;
using NicheImageRipper.Sdk.Utility;
using NicheImageRipper.SiteModules.Modules.Google;
using Serilog;

namespace SiteModules.Tests;

/// <summary>
///     Integration tests against the real Google Drive API. They're opt-in and skip unless set up:
///     <list type="bullet">
///         <item><c>NIR_GDRIVE_TEST_FOLDER_URL</c>: a Drive folder URL your account can read, with at least one file.</item>
///         <item><c>client_secrets.json</c> in the Workspace folder (the token cache lands there too). The first run
///         may open a browser to sign in.</item>
///     </list>
/// </summary>
[Trait("Category", "Integration")]
public class GDriveTest
{
    private const string FolderUrlVariable = "NIR_GDRIVE_TEST_FOLDER_URL";

    private readonly ITestOutputHelper _output;

    public GDriveTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task QueryGDriveLinksTest()
    {
        var folderUrl = RequireSetup();

        var (links, nextIndex) = await GDriveHelper.QueryGDriveLinks(folderUrl, 0, FilenameScheme.Original);

        Assert.NotEmpty(links);
        Assert.Equal(links.Count, nextIndex);
        Assert.All(links, link => Assert.Equal(GDriveLinkInfo.GDrive, link.LinkInfo));
        _output.WriteLine($"Found {links.Count} links:");
        foreach (var link in links)
        {
            _output.WriteLine($"{link.Url}  {link.Filename}");
        }
    }

    [Fact]
    public async Task DownloadGDriveFileTest()
    {
        var folderUrl = RequireSetup();
        var (links, _) = await GDriveHelper.QueryGDriveLinks(folderUrl, 0, FilenameScheme.Original);
        Assert.NotEmpty(links);

        var path = Path.Combine(CreateOutputDirectory("GDriveTestDownloadFile"), links[0].Filename);
        var result = await new GDriveDownloadStrategy().DownloadAsync(links[0], path, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(DownloadOutcome.Success, result.Outcome);
        Assert.True(new FileInfo(path).Length > 0, $"Downloaded file is empty: {path}");
    }

    [Fact]
    public async Task DownloadGDriveFolderTest()
    {
        var folderUrl = RequireSetup();
        var (links, _) = await GDriveHelper.QueryGDriveLinks(folderUrl, 0, FilenameScheme.Original);
        Assert.NotEmpty(links);

        var directory = CreateOutputDirectory("GDriveTestDownloadFolder");
        var strategy = new GDriveDownloadStrategy();
        foreach (var link in links)
        {
            var path = Path.Combine(directory, link.Filename);
            var result = await strategy.DownloadAsync(link, path, CreateContext(), TestContext.Current.CancellationToken);
            Assert.Equal(DownloadOutcome.Success, result.Outcome);
        }
    }

    /// <summary>Skips the test unless it's configured, and points the app data folder at Workspace (the credentials).</summary>
    private static string RequireSetup()
    {
        var folderUrl = Environment.GetEnvironmentVariable(FolderUrlVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(folderUrl), $"Set {FolderUrlVariable} to run Google Drive tests");

        var workspace = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Workspace"));
        Assert.SkipUnless(File.Exists(Path.Combine(workspace, "client_secrets.json")),
            $"Google Drive tests need client_secrets.json in {workspace}");
        AppData.Directory = workspace;
        return folderUrl!;
    }

    private static string CreateOutputDirectory(string name)
    {
        return AppData.EnsureDirectory("TestFiles", name);
    }

    /// <summary>
    ///     The Drive strategy authenticates and downloads on its own and never reads the context, so only the
    ///     simple members are filled in; there's no browser behind <see cref="DownloadContext.WebDriver"/>.
    /// </summary>
    private static DownloadContext CreateContext() => new()
    {
        RequestHeaders = new Dictionary<string, string>(),
        WebDriver = null!,
        Session = new HttpClient(),
        SiteName = "gdrive",
        SleepTime = 0,
        Logger = Log.Logger,
        HeaderModifiers = [],
        ErrorHandlers = [],
        PostDownloadValidators = [],
    };
}
