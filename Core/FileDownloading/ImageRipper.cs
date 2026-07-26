using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.PartialSaves;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using Serilog;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.FileDownloading;

public partial class ImageRipper : IDisposable
{
    internal const string RipIndexPath = ".ripIndex";

    private const string YoutubeCookiesFile = "yt_cookies.txt";
    private const string RipStatePath = "ripState.json";
    private const int RetryCount = 4;
    private const int MillisecondsInSecond = 1000;
    private const int MinimumFileSize = 1024; // 1KB minimum file size

    internal static ApiClientManager ClientManager { get; } = new();

    // ReSharper disable once UnusedMember.Local
    private static readonly string[] MediaExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".bmp",
        ".tiff",
        ".tif",
        ".webp",
        ".heic",
        ".heif",
        ".avif",
        ".ico",
        ".psd",
        ".raw",
        ".cr2",
        ".cr3",
        ".nef",
        ".arw",
        ".dng",
        ".orf",
        ".rw2",
        ".svg",
        ".ai",
        ".eps",
        ".pdf",
        ".mp4",
        ".mkv",
        ".mov",
        ".avi",
        ".wmv",
        ".flv",
        ".webm",
        ".m4v",
        ".mpg",
        ".mpeg",
        ".3gp",
        ".ts",
        ".mts",
        ".m2ts",
        ".ogv",
        ".prores",
        ".dnxhd",
        ".braw",
        ".r3d",
        ".cine",
        ".xf",
        ".mp3",
        ".aac",
        ".ogg",
        ".m4a",
        ".wma",
        ".opus",
        ".flac",
        ".wav",
        ".aiff",
        ".alac"
    ];

    private Dictionary<string, string> RequestHeaders { get; } = new()
    {
        [RequestHeaderKeys.UserAgent] = Config.UserAgent,
        [RequestHeaderKeys.Referer] = "",
        [RequestHeaderKeys.Cookie] = "",
    };

    private FilenameScheme FilenameScheme { get; }
    private UnzipProtocol UnzipProtocol { get; }
    public RipInfo FolderInfo { get; private set; } = null!;
    public PostDownloadAction PostDownloadAction { get; set; }
    private string GivenUrl { get; set; }
    private bool Interrupted { get; set; }
    private Dictionary<string, bool> PersistentLogins { get; }
    private string SavePath { get; }
    private HttpClient Session { get; }
    private string SiteName { get; set; }
    private float SleepTime { get; set; }
    public int CurrentIndex { get; private set; }
    private double FailureThreshold { get; set; } = 0.5;
    private WebDriverPool DriverPool { get; }
    private WebDriver WebDriver { get; set; }
    private ILogger Logger { get; }
    public bool Paused { get; set; }

    private FileDownloadStrategyRegistry StrategyRegistry { get; }
    private IReadOnlyList<IRequestHeaderModifier> HeaderModifiers { get; }
    private IReadOnlyList<IDownloadErrorHandler> ErrorHandlers { get; }
    private IReadOnlyList<IPostDownloadValidator> PostDownloadValidators { get; }
    private IReadOnlyList<IExternalToolDownloadStrategy> ExternalToolStrategies { get; }
    private DownloadContext DownloadContext { get; set; } = null!;
    private bool ClearPartialSaveOnFailureThreshold { get; }

    private bool _disposed;

    private FirefoxDriver Driver => WebDriver.Driver;

    private static GeneralConfig Config => Configuration.Config.Instance;
    private static TokenManager TokenManager => TokenManager.Instance;
    private static FlareSolverrManager FlareSolverrManager => NicheImageRipper.FlareSolverrManager;

    public delegate void ProgressChangedHandler(int current, int total);

    public event ProgressChangedHandler? OnProgressChanged;

    public ImageRipper(WebDriverPool driverPool, FilenameScheme filenameScheme = FilenameScheme.Original,
                       UnzipProtocol unzipProtocol = UnzipProtocol.None,
                       PostDownloadAction postDownloadAction = PostDownloadAction.None,
                       bool clearPartialSaveOnFailureThreshold = true)
    {
        FilenameScheme = filenameScheme;
        UnzipProtocol = unzipProtocol;
        PostDownloadAction = postDownloadAction;
        ClearPartialSaveOnFailureThreshold = clearPartialSaveOnFailureThreshold;
        GivenUrl = "";
        Interrupted = false;
        PersistentLogins = new Dictionary<string, bool>();
        // Create copy of save path, so that config can be changed without affecting current ripper instance
        SavePath = Config.SavePath;
        Session = new HttpClient();
        SiteName = "";
        SleepTime = 0.2f;
        CurrentIndex = 0;
        DriverPool = driverPool;
        WebDriver = driverPool.AcquireDriver(true);
        Logger = Log.ForContext<ImageRipper>();

        StrategyRegistry = DownloadCompositionRoot.BuildStrategyRegistry();
        HeaderModifiers = DownloadCompositionRoot.BuildHeaderModifiers();
        ErrorHandlers = DownloadCompositionRoot.BuildErrorHandlers();
        PostDownloadValidators = DownloadCompositionRoot.BuildPostDownloadValidators();
        ExternalToolStrategies = DownloadCompositionRoot.BuildExternalToolStrategies();
    }

    public async Task Rip(string url, CancellationToken cancellationToken = default)
    {
        // Cannot rip if the instance is disposed, but everything else is fine to access
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ImageRipper), "Cannot use a disposed ImageRipper instance");
        }

        OnProgressChanged?.Invoke(0, 0); // Indeterminate progress at the start

        // TODO: Site-specific cookie injection (pre-parse) was never fully ported from the Python version.
        //  Revisit once parser support for this is rebuilt — CookiesNeeded/AddCookies removed as dead stubs.
        
        
        GivenUrl = url.Replace("members.", "www."); // Hanime-specific — inline until a second site needs the same normalization
        (SiteName, SleepTime) = UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        Logger.Debug("Site Name: {SiteName}", SiteName);

        await FileGetter(cancellationToken);
    }

    private void LoadCorrectWebDriver()
    {
        // TODO: Figure out better implementation for this
        if (SiteName == "quatvn")
        {
            if (WebDriver.IsHeadless)
            {
                Logger.Debug("Releasing headless driver");
                DriverPool.ReleaseDriver(WebDriver);
                // Need non-headless driver for quatvn
                Logger.Debug("Acquiring non-headless driver");
                WebDriver = DriverPool.AcquireDriver(false);
            }
        }
        else
        {
            if (!WebDriver.IsHeadless)
            {
                Logger.Debug("Releasing non-headless driver");
                DriverPool.ReleaseDriver(WebDriver);
                // Can use headless driver for other sites
                Logger.Debug("Acquiring headless driver");
                WebDriver = DriverPool.AcquireDriver(true);
            }
        }
    }

    private async Task<int> GetStartIndex(CancellationToken cancellationToken = default)
    {
        int start;
        if (File.Exists(RipIndexPath))
        {
            (var saveUrl, start) = await CacheUtility.ReadRipIndex(RipIndexPath, cancellationToken);
            if (saveUrl == GivenUrl)
            {
                Logger.Information("Resuming from index {StartIndex}", start);
                File.Delete(RipIndexPath);
            }
            else
            {
                start = FolderInfo.MustGenerateManually ? 1 : 0;
            }
        }
        else
        {
            start = FolderInfo.MustGenerateManually ? 1 : 0;
        }

        return start;
    }

    private DownloadContext BuildDownloadContext() => new()
    {
        RequestHeaders = RequestHeaders,
        ClientManager = ClientManager,
        WebDriver = WebDriver,
        Session = Session,
        SiteName = SiteName,
        SleepTime = SleepTime,
        Logger = Logger,
        HeaderModifiers = HeaderModifiers,
        ErrorHandlers = ErrorHandlers,
        PostDownloadValidators = PostDownloadValidators,
    };

    private async Task FileGetter(CancellationToken cancellationToken = default)
    {
        LoadCorrectWebDriver();
        DownloadContext = BuildDownloadContext();

        var htmlParser = HtmlParser.GetParser(SiteName, WebDriver, ClientManager, RequestHeaders, FilenameScheme);
        Logger.Debug("Constructed HtmlParser");
        FolderInfo = await htmlParser.ParseSite(GivenUrl, cancellationToken);
        var fullPath = Path.Combine(SavePath, FolderInfo.DirectoryName);
        if (Interrupted && FilenameScheme != FilenameScheme.Hash)
        {
            // TODO: self.folder_info.urls = self.get_incomplete_files(full_path)
        }

        Logger.Debug("Dir Length: {DirLength}", fullPath.Length);
        Directory.CreateDirectory(fullPath);

        var start = await GetStartIndex(cancellationToken);
        var (downloadStats, filesHashes) = await LoadOrCreateRipState(start, cancellationToken);

        try
        {
            switch (FolderInfo.DownloadMode)
            {
                case DownloadMode.Generate:
                    await HandleGeneratingManually(start, fullPath, filesHashes, downloadStats, cancellationToken);
                    break;
                case DownloadMode.ExternalTool:
                    await HandleExternalToolDownload(fullPath, downloadStats, cancellationToken);
                    break;
                case DownloadMode.List:
                default: // forward-compat fallback for any future DownloadMode value
                    await HandleDownloadingFromList(start, fullPath, filesHashes, downloadStats, cancellationToken);
                    break;
            }
        }
        catch (Exception e)
        {
            Logger.Debug("Saving rip state due to exception. Reason: {Message}", e.Message);
            SaveRipStateOnFailure(downloadStats, filesHashes);
            throw;
        }

        await FinalizeRip(fullPath, downloadStats, cancellationToken);
    }

    private async Task<(DownloadStats DownloadStats, IndexedHashes FilesHashes)> LoadOrCreateRipState(
        int start, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(RipStatePath))
        {
            return (new DownloadStats(), new IndexedHashes());
        }

        var state = JsonUtility.Deserialize<RipState>(RipStatePath);
        if (state is null)
        {
            Logger.Error("Failed to load rip state");
            throw new RipperException("Failed to load rip state");
        }

        var filesHashes = IndexedHashes.Deserialize(state.FilesHashes);
        Logger.Debug("Truncating files hashes to index {StartIndex}", start);
        filesHashes.TruncateToIndex(start);

        return (state.DownloadStats, filesHashes);
    }

    private void SaveRipStateOnFailure(DownloadStats downloadStats, IndexedHashes filesHashes)
    {
        var state = new RipState
        {
            DownloadStats = downloadStats,
            FilesHashes = filesHashes.Serialize(),
        };

        JsonUtility.Serialize(RipStatePath, state);
        Logger.Debug("Saved rip state to {RipStatePath}", RipStatePath);
    }

    private async Task FinalizeRip(string fullPath, DownloadStats downloadStats,
                                   CancellationToken cancellationToken = default)
    {
        File.Delete(RipIndexPath);
        File.Delete(RipStatePath); // Existence of rip state file indicates incomplete rip

        if (((double)downloadStats.FailedDownloadsCount) / FolderInfo.NumUrls > FailureThreshold)
        {
            var msg = $"More than {FailureThreshold * 100}% of the images failed to download";
            var e = new RipperException(msg);
            Logger.Error(e, "{msg:l}", msg);
            PartialSaveManager.Instance.RemovePartialSave(GivenUrl);
            throw e;
        }

        if (UnzipProtocol != UnzipProtocol.None)
        {
            UnzipFiles(fullPath, downloadStats);
        }

        if (downloadStats.HasFailedDownloads)
        {
            var failedDownloads = downloadStats.FailedDownloads.Join("\n");
            var failedPath = Path.Combine(fullPath, "failedDownloads.txt");
            await File.WriteAllTextAsync(failedPath, failedDownloads, cancellationToken);
        }

        var downloadResults = downloadStats.GetStats(FolderInfo.NumUrls);
        Logger.Information("{Results:l}", downloadResults);
        Logger.Information("Download Complete");
        OnProgressChanged?.Invoke(1, 1);
    }

    private static Task Sleep(int milliseconds, CancellationToken cancellationToken = default)
    {
        return Task.Delay(milliseconds, cancellationToken);
    }

    private async Task HandleGeneratingManually(int start, string fullPath, IndexedHashes filesHashes,
                                                DownloadStats downloadStats,
                                                CancellationToken cancellationToken = default)
    {
        // Gets the general url for all images in this album
        var imageLink = FolderInfo.Urls[0];
        var trimmedUrl = TrimUrl(imageLink.Url);
        imageLink.Url = trimmedUrl;
        string[] extensions = [".webp", ".jpg", ".gif", ".png", ".webm", ".mp4", "t.jpg"];

        // Downloads all images from the general url by incrementing the file number
        //  (e.g., https://domain/gallery/##.jpg)
        for (var index = start; index < FolderInfo.NumUrls + 1; index++)
        {
            CurrentIndex = index;
            OnProgressChanged?.Invoke(index, FolderInfo.NumUrls + 1);

            while (Paused)
            {
                await Sleep(1000, cancellationToken);
            }

            foreach (var (i, ext) in extensions.Enumerate())
            {
                try
                {
                    var fullFilename = $"{index}{ext}";
                    var imagePath = Path.Combine(fullPath, fullFilename);
                    var skipDownload = new Box<bool>(false);
                    var success = await DownloadFromUrl(imageLink, index.ToString(), imagePath, ext, skipDownload,
                        cancellationToken);
                    if (success && !skipDownload)
                    {
                        await PostProcess(imageLink, imagePath, filesHashes, downloadStats, index, cancellationToken);
                    }

                    break;
                }
                catch // TODO: Narrow down exceptions
                {
                    if (i == 3)
                    {
                        downloadStats.FailedDownloads.Add($"{imageLink.Url}{index}.{ext}]");
                        Logger.Warning("Image not found");
                    }
                }
            }
        }
    }

    // Refactored
    private async Task HandleExternalToolDownload(string fullPath, DownloadStats downloadStats,
                                                  CancellationToken cancellationToken = default)
    {
        var strategy = ExternalToolStrategies.FirstOrDefault(s => s.AppliesTo(SiteName));
        if (strategy is null)
        {
            throw new RipperException($"No external tool download strategy registered for site: {SiteName}");
        }

        var result = await strategy.DownloadAsync(FolderInfo, fullPath, DownloadContext, cancellationToken);
        if (result.Outcome == DownloadOutcome.Failed)
        {
            // TODO: Handle failed downloads for external tools (e.g., log failed URLs)
            downloadStats.FailedDownloads.Add(FolderInfo.Urls[0].Url);
        }
    }

    // Refactored
    private async Task HandleDownloadingFromList(int start, string fullPath, IndexedHashes filesHashes,
                                                 DownloadStats downloadStats,
                                                 CancellationToken cancellationToken = default)
    {
        var completed = new bool[FolderInfo.NumUrls];
        while (true)
        {
            try
            {
                if (FolderInfo.MaxConcurrentDownloads is { } maxConcurrent)
                {
                    await DownloadConcurrently(maxConcurrent, completed, fullPath, filesHashes, downloadStats,
                        cancellationToken);
                }
                else
                {
                    await DownloadSequentially(start, fullPath, filesHashes, downloadStats, cancellationToken);
                }

                break;
            }
            catch (UrlExpiredException e)
            {
                Logger.Information("Refreshing links for {SiteName}", e.SiteName);
                var parser = HtmlParser.GetParser(e.SiteName, WebDriver, ClientManager, RequestHeaders, FilenameScheme);
                if (parser is not TimeSensitiveHtmlParser timeSensitiveParser)
                {
                    throw new RipperException($"Parser for {e.SiteName} does not support link refreshing");
                }

                start = e.ResumeIndex;
                FolderInfo.Urls = await timeSensitiveParser.UpdateLinks(FolderInfo.Urls, start, cancellationToken);
            }
        }
    }

    // Refactored
    private async Task DownloadConcurrently(int maxConcurrent, bool[] completed, string fullPath,
                                            IndexedHashes filesHashes, DownloadStats downloadStats,
                                            CancellationToken cancellationToken)
    {
        var total = FolderInfo.NumUrls;
        var complete = 0;
        OnProgressChanged?.Invoke(1, total + 1);
        const int maxAttempts = 4;
        var semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);

        var tasks = FolderInfo.Urls.Select(async (link, i) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            if (completed[i])
            {
                semaphore.Release();
                return;
            }

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                while (Paused)
                {
                    await Sleep(1000, cancellationToken);
                }

                try
                {
                    await DownloadSingleFromList(i, link, fullPath, filesHashes, downloadStats,
                        cancellationToken: cancellationToken);
                    Interlocked.Increment(ref complete);
                    completed[i] = true;
                    Logger.Information("Finished downloading {Index}, {Total} remaining", i + 1, total - complete);
                    OnProgressChanged?.Invoke(complete + 1, total + 1);
                    break;
                }
                catch (Exception e)
                {
                    if (attempt == maxAttempts - 1)
                    {
                        Logger.Error(e, "Error downloading {Index}, {Total} remaining: {Url}", i + 1, total - complete,
                            link.Url);
                    }
                    else
                    {
                        Logger.Warning(
                            "Error downloading {Index}, {Total} remaining: {Url}, retrying... ({Attempt}/{MaxAttempts})",
                            i + 1, total - complete, link.Url, attempt + 1, maxAttempts);
                        await Sleep(1000, cancellationToken);
                    }
                }
            }

            semaphore.Release();
        });

        await Task.WhenAll(tasks);
    }

    // Refactored
    private async Task DownloadSequentially(int start, string fullPath, IndexedHashes filesHashes,
                                            DownloadStats downloadStats, CancellationToken cancellationToken)
    {
        foreach (var (i, link) in FolderInfo.Urls.Skip(start).Enumerate())
        {
            while (Paused)
            {
                await Sleep(1000, cancellationToken);
            }

            var index = start + i;
            await DownloadSingleFromList(index, link, fullPath, filesHashes, downloadStats, true, cancellationToken);
        }
    }

    // Refactored
    private async Task DownloadSingleFromList(int index, FileLink link, string fullPath, IndexedHashes filesHashes,
                                              DownloadStats downloadStats, bool updateProgress = false,
                                              CancellationToken cancellationToken = default)
    {
        Logger.Debug("Index: {Index}, Total: {Total}", index, FolderInfo.NumUrls);
        if (updateProgress)
        {
            OnProgressChanged?.Invoke(index + 1, FolderInfo.NumUrls + 1);
        }

        CurrentIndex = index;
        while (Paused)
        {
            await Sleep(1000, cancellationToken);
        }

        await Sleep((int)SleepTime * MillisecondsInSecond, cancellationToken);
        try
        {
            var filename = link.Filename;
            var imagePath = Path.Combine(fullPath, filename);
            var result = await DownloadFromList(link, imagePath, index, downloadStats, cancellationToken);
            if (result.Outcome == DownloadOutcome.Success)
            {
                // DownloadFromList may modify filename (if it was missing extension)
                imagePath = Path.Combine(fullPath, link.Filename);
                await PostProcess(link, imagePath, filesHashes, downloadStats, index, cancellationToken);
            }
        }
        catch (FileNotFoundException)
        {
            Logger.Warning("File not found: {Url}", link.Url);
            if (link.LinkInfo == LinkInfo.IframeMedia)
            {
                downloadStats.FailedDownloads.Add(link.Url);
                await File.AppendAllTextAsync("failed_iframe.txt", $"{link.Url} {link.Referer}\n", cancellationToken);
            }
        }
        catch (UrlExpiredException e)
        {
            Logger.Information("Caught UrlExpiredException, need to refresh links");
            e.ResumeIndex = index;
            throw;
        }
        catch (Exception e)
        {
            if (e.Message.Contains("see inner exception"))
            {
                Logger.Debug("Caught exception with inner exception while downloading {Url}: {InnerException}",
                    link.Url, e.InnerException?.Message);
            }
            else
            {
                Logger.Debug("Caught exception, saving progress. Reason: {ErrorMessage}", e.Message);
            }

            await SaveCurrentRipPosition(cancellationToken);
            throw;
        }
    }

    internal Task SaveCurrentRipPosition(CancellationToken cancellationToken = default)
    {
        return CacheUtility.SaveRipIndex(RipIndexPath, GivenUrl, CurrentIndex, cancellationToken);
    }

    internal static async Task IncrementCurrentRipPosition(CancellationToken cancellationToken = default)
    {
        var (url, index) = await CacheUtility.ReadRipIndex(RipIndexPath, cancellationToken);
        index++;
        await CacheUtility.SaveRipIndex(RipIndexPath, url, index, cancellationToken);
    }

    private async Task PostProcess(FileLink link, string imagePath, IndexedHashes filesHashes,
                                   DownloadStats downloadStats, int index,
                                   CancellationToken cancellationToken = default)
    {
        if (PostDownloadAction.HasFlag(PostDownloadAction.RemoveDuplicates))
        {
            // Maybe handle directory downloads (e.g., Mega) in the future?
            // Unable to determine filename before downloading files/directories from Mega.nz
            // Could probably guess file ext for YouTube videos though (as we have filestem already)
            if (Directory.Exists(imagePath))
            {
                return;
            }

            if (!StrategyRegistry.Resolve(link.LinkInfo).SupportsPostProcessing)
            {
                return;
            }

            var duplicate = await HandleDuplicateFile(imagePath, filesHashes, index, cancellationToken);
            if (duplicate)
            {
                downloadStats.NumDuplicates++;
            }
        }
    }

    private async Task<bool> HandleDuplicateFile(string imagePath, IndexedHashes filesHashes, int index,
                                                 CancellationToken cancellationToken = default)
    {
        var fileHash = await FileUtility.GetFileHash(imagePath, cancellationToken);
        if (!filesHashes.Add(fileHash, index))
        {
            Logger.Information("Duplicate file detected: {ImagePath}", imagePath);
            File.Delete(imagePath);
            return true;
        }

        Logger.Debug("File hash: {FileHash}", fileHash);
        return false;
    }

    /// <summary>
    ///     Download image from image url
    /// </summary>
    /// <param name="fileLink">ImageLink containing data on the file to download</param>
    /// <param name="filename">Name of the file to download</param>
    /// <param name="imagePath">Full path to download the file to</param>
    /// <param name="ext">Extension of the file to download</param>
    /// <param name="skipDownload"></param>
    /// <param name="cancellationToken">Cancellation token to cancel the download operation</param>
    private async Task<bool> DownloadFromUrl(FileLink fileLink, string filename, string imagePath, string ext,
                                             Box<bool> skipDownload, CancellationToken cancellationToken = default)
    {
        var numFiles = FolderInfo.NumUrls;
        // Completes the specific image URL from the general URL
        var url = fileLink.Url;
        var fullFilename = $"{filename}{ext}";
        var ripUrl = $"{url}{fullFilename}";
        var numProgress = $"({filename}/{numFiles})";
        Logger.Information("{RipUrl:l}    {NumProgress:l}", ripUrl, numProgress);
        fileLink.Url = ripUrl;
        bool success;
        try
        {
            success = await DownloadFile(imagePath, fileLink, true, skipDownload, cancellationToken);
        }
        finally
        {
            fileLink.Url = url;
        }

        await Sleep(50, cancellationToken);
        return success;
    }

    // Refactored
    /// <summary>
    ///     Download images from url supplied from a list of image urls
    /// </summary>
    /// <param name="link"><see cref="FileLink"/> containing data on the file to download</param>
    /// <param name="imagePath">Full path of the location to save the file to</param>
    /// <param name="currentFileNum">Number of the file being downloaded</param>
    /// <param name="downloadStats">DownloadStats object to update with results</param>
    /// <param name="cancellationToken">Cancellation token to cancel the download operation</param>
    private async Task<DownloadResult> DownloadFromList(FileLink link, string imagePath, int currentFileNum,
                                                        DownloadStats downloadStats,
                                                        CancellationToken cancellationToken = default)
    {
        var numFiles = FolderInfo.NumUrls;
        var ripUrl = link.Url;
        var displayUrl = link.LinkInfo == LinkInfo.Base64 ? UrlUtility.TruncateLongUrl(ripUrl) : ripUrl;
        Logger.Information("{Url:l}    ({CurrentProgress}/{TotalProgress})", displayUrl, currentFileNum + 1, numFiles);

        var oldReferer = RequestHeaders[RequestHeaderKeys.Referer];
        if (link.HasReferer)
        {
            RequestHeaders[RequestHeaderKeys.Referer] = link.Referer;
        }
        else if (link.Referer is null)
        {
            RequestHeaders[RequestHeaderKeys.Referer] = "";
        }

        var strategy = StrategyRegistry.Resolve(link.LinkInfo);
        var result = await strategy.DownloadAsync(link, imagePath, DownloadContext, cancellationToken);

        if (result.Outcome == DownloadOutcome.Failed)
        {
            if (Config.SkipFailedDownloads)
            {
                downloadStats.FailedDownloads.Add(ripUrl);
            }
            else
            {
                throw new RipperException("Failed to download file: " + ripUrl +
                                          (result.Reason is null ? "" : $" ({result.Reason})"));
            }
        }

        if (link.LinkInfo == LinkInfo.YoutubeVideo)
        {
            await Sleep(1250, cancellationToken);
        }

        RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
        await Sleep(50, cancellationToken);

        return result;
    }

    private async Task<bool> ResolveAndDownloadFile(string path, FileLink fileLink, Box<bool> skipDownload,
                                                    CancellationToken cancellationToken = default)
    {
        var url = fileLink.Url;
        for (var i = 0; i < RetryCount; i++)
        {
            var imageUrl = await GetDownloadUrl(url, cancellationToken);
            if (imageUrl == "")
            {
                await Sleep(500, cancellationToken);
                continue;
            }

            Logger.Debug("Resolved URL: {Url}", imageUrl);
            fileLink.Url = imageUrl;

            var success = await DownloadFile(path, fileLink, false, skipDownload, cancellationToken);
            if (success)
            {
                return true;
            }

            await Sleep(500, cancellationToken);
        }

        return false;
    }

    private async Task<string> GetDownloadUrl(string url, CancellationToken cancellationToken = default)
    {
        var siteName = url.Split('.')[1];
        using var request = RequestHeaders.ToRequest(HttpMethod.Get, url);
        var response = await Session.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Logger.Error("<Response {ErrorCode}> Failed to get download url: {Url}", response.StatusCode, url);
            return "";
        }

        if (response.RequestMessage!.RequestUri!.ToString() == $"https://www.{siteName}.com/hcaptcha.aspx")
        {
            Logger.Information("Captcha detected, solving...");
            await SolveCaptcha(url, true, cancellationToken);
            var reRequest = RequestHeaders.ToRequest(HttpMethod.Get, url);
            response = await Session.SendAsync(reRequest, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Error("Failed to get download url: {Url}", url);
                return "";
            }
        }

        Logger.Information("Getting download url from {Url}", url);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var match = NLegsImageUrlRegex().Match(content);
        return $"https://www.{siteName}.com" + match.Groups[1].Value;
    }

    private async Task SolveCaptcha(string url, bool humanSolve, CancellationToken cancellationToken = default)
    {
        await FlareSolverrManager.GetSiteSolution(url, cancellationToken: cancellationToken);
        if (humanSolve)
        {
            Logger.Information("Solve the captcha and press enter to continue");
            Console.ReadLine();
        }
    }

    public static void CopyFolder(string sourceFolder, string destinationRoot)
    {
        if (!Directory.Exists(sourceFolder))
        {
            throw new DirectoryNotFoundException(sourceFolder);
        }

        var folderName = Path.GetFileName(sourceFolder.TrimEnd(Path.DirectorySeparatorChar));
        var destinationFolder = Path.Combine(destinationRoot, folderName);

        CopyRecursive(sourceFolder, destinationFolder);
    }

    private static void CopyRecursive(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: false);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(destination, Path.GetFileName(dir));
            CopyRecursive(dir, destDir);
        }
    }

    public static string CopyWithIncrement(string sourceFilePath, string destinationFolder)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Source file not found.", sourceFilePath);
        }

        if (!Directory.Exists(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        var originalFileName = Path.GetFileNameWithoutExtension(sourceFilePath);
        var extension = Path.GetExtension(sourceFilePath);

        var destinationPath = Path.Combine(destinationFolder, originalFileName + extension);

        var counter = 1;

        while (File.Exists(destinationPath))
        {
            var newFileName = $"{originalFileName} ({counter}){extension}";
            destinationPath = Path.Combine(destinationFolder, newFileName);
            counter++;
        }

        File.Copy(sourceFilePath, destinationPath);

        return destinationPath;
    }

    /// <summary>
    ///     Downloads a file from the specified image link.
    /// </summary>
    /// <param name="filePath">The path where the file will be saved.</param>
    /// <param name="fileLink">The link metadata of the file to download.</param>
    /// <param name="generatingManually">Indicates whether the download links are being generated manually.</param>
    /// <param name="skipDownload">Whether this download is being skipped due to being undownloadable for various reasons</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous download operation. The task result contains a boolean indicating whether the download was successful.</returns>
    private async Task<bool> DownloadFile(string filePath, FileLink fileLink, bool generatingManually,
                                          Box<bool> skipDownload, CancellationToken cancellationToken = default)
    {
        if (filePath[^1] == '/')
        {
            filePath = filePath[..^1];
        }

        var success = false;
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            success = await DownloadFileHelper(fileLink, filePath, generatingManually, skipDownload,
                cancellationToken);
            if (success)
            {
                break;
            }
        }

        if (!success)
        {
            return false; // Failed to download file
        }

        // If the downloaded file doesn't have an extension for some reason, search for correct ext
        if (Path.GetExtension(filePath) == "" && !skipDownload)
        {
            Logger.Debug("Finding correct extension for file: {ImagePath}", filePath);
            var extension = FileUtility.GetCorrectExtension(filePath);
            await RenameFile(filePath, filePath + extension, cancellationToken);
            var filename = Path.GetFileName(filePath);
            var newFilename = filename + extension;
            Logger.Debug("Renamed file {OldFilename} to {NewFilename}", filename, newFilename);
            fileLink.Filename = newFilename;
        }

        return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileLink"></param>
    /// <param name="imagePath"></param>
    /// <param name="generatingManually"></param>
    /// <param name="skipDownload">Whether this download is being skipped due to being undownloadable for various reasons</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="EHentaiUrlExpiredException"></exception>
    /// <exception cref="RipperException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private async Task<bool> DownloadFileHelper(FileLink fileLink, string imagePath, bool generatingManually,
                                                Box<bool> skipDownload, CancellationToken cancellationToken = default)
    {
        if (fileLink.IsInvalid)
        {
            if (SiteName == "e-hentai")
            {
                // Used to force generation of unparsed links
                // Using a partial parsing approach, so this may be called multiple times per rip
                throw new UrlExpiredException(SiteName);
            }

            // This should never happen for any other site
            throw new RipperException("Invalid ImageLink found for non-EHentai site");
        }

        var url = fileLink.Url;
        await Task.Delay((int)(SleepTime * MillisecondsInSecond), cancellationToken);
        var (modifiedHeader, oldCookies) = await ModifyHeaders(url, fileLink, cancellationToken);

        Logger.Debug("Request Headers: {@RequestHeaders}", RequestHeaders);
        var resumeFrom = 0L;
        while (true)
        {
            HttpResponseMessage response;
            try
            {
                using var request = RequestHeaders.ToRequest(HttpMethod.Get, url);
                if (resumeFrom > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(resumeFrom, null);
                    Logger.Information("Resuming download from byte {Offset}", resumeFrom);
                }

                response = await Session.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            }
            catch (HttpRequestException e) when (e.InnerException is InvalidOperationException)
            {
                Logger.Error("Unable to establish a connection to {Url}", url);
                return false;
            }
            catch (HttpRequestException e) when (e.InnerException is SocketException)
            {
                Logger.Error("Unable to establish a connection to {Url}", url);
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                return await HandleUnsuccessfulStatusCode(response, url, fileLink, generatingManually, skipDownload,
                    cancellationToken);
            }

            DownloadStatus result;
            try
            {
                result = await WriteToFile(response, imagePath, resumeFrom, cancellationToken);
            }
            catch (DownloadTimeoutException e)
            {
                if (e.DownloadedBytesCount == resumeFrom)
                {
                    Logger.Warning("No progress made during download, aborting...");
                    throw;
                }

                resumeFrom += e.DownloadedBytesCount;
                continue;
            }
            catch (Exception e)
            {
                Logger.Debug("Exception during file write: {Exception}", e);
                throw;
            }

            switch (result)
            {
                case DownloadStatus.None:
                    // Should not occur
                    break;
                case DownloadStatus.Ok:
                    break;
                case DownloadStatus.ConnectionReset:
                    return false;
                case DownloadStatus.Failed:
                    LogFailedUrl(url);
                    return false;
                default:
                    throw new ArgumentOutOfRangeException($"Enum value not handled: {result}");
            }

            RestoreHeaders(modifiedHeader, oldCookies);

            if (fileLink.LinkInfo == LinkInfo.GoFile)
            {
                var ext = FileUtility.GetCorrectExtension(imagePath);
                if (ext != ".html")
                {
                    return true;
                }

                Logger.Warning("GoFile download failed, trying again...");
                await AssociateGoFileCookies(fileLink.Url, cancellationToken);
                return false;
            }

            return true;
        }
    }

    private async Task<(ModifiedHeader modifiedHeader, string oldCookies)> ModifyHeaders(
        string url, FileLink fileLink, CancellationToken cancellationToken = default)
    {
        var modifiedHeader = ModifiedHeader.None;
        var oldCookies = "";
        if (url.Contains("redgifs"))
        {
            modifiedHeader = ModifiedHeader.Authorization;
            var token = await TokenManager.GetToken(TokenKey.Redgifs, cancellationToken);
            RequestHeaders[RequestHeaderKeys.Authorization] = $"Bearer {token.Value}";
        }
        else if (fileLink.LinkInfo == LinkInfo.GoFile)
        {
            modifiedHeader = ModifiedHeader.Cookie;
            var cookieValue = Config.Custom.GoFile.AccountToken;
            var cookie = $"accountToken={cookieValue}";
            oldCookies = RequestHeaders[RequestHeaderKeys.Cookie];
            RequestHeaders[RequestHeaderKeys.Cookie] = cookie;
        }
        else if (fileLink.Url.Contains("donmai.us"))
        {
            modifiedHeader = ModifiedHeader.UserAgent;
            RequestHeaders[RequestHeaderKeys.UserAgent] = "NicheImageRipper";
        }

        return (modifiedHeader, oldCookies);
    }

    private void RestoreHeaders(ModifiedHeader modifiedHeader, string oldCookies)
    {
        if (modifiedHeader.HasFlag(ModifiedHeader.Authorization))
        {
            RequestHeaders.Remove(RequestHeaderKeys.Authorization);
        }
        else if (modifiedHeader.HasFlag(ModifiedHeader.Cookie))
        {
            // Add more logic here if other sites require cookies when downloading files
            RequestHeaders[RequestHeaderKeys.Cookie] = oldCookies;
        }
        else if (modifiedHeader.HasFlag(ModifiedHeader.UserAgent))
        {
            RequestHeaders[RequestHeaderKeys.UserAgent] = Config.UserAgent;
        }
    }

    private static readonly Dictionary<string, string> PixivExtMap = new()
    {
        ["jpg"] = "png",
        ["png"] = "gif",
    };

    /// <summary>
    ///     Handles unsuccessful HTTP response status codes during file download attempts.
    /// </summary>
    /// <param name="response"></param>
    /// <param name="url"></param>
    /// <param name="fileLink"></param>
    /// <param name="generatingManually"></param>
    /// <param name="skipDownload">Whether this download is being skipped due to being undownloadable for various reasons</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="WrongExtensionException"></exception>
    /// <exception cref="BadSubdomainException"></exception>
    /// <exception cref="EHentaiUrlExpiredException"></exception>
    private async Task<bool> HandleUnsuccessfulStatusCode(HttpResponseMessage response, string url, FileLink fileLink,
                                                          bool generatingManually, Box<bool> skipDownload,
                                                          CancellationToken cancellationToken = default)
    {
        Logger.Warning("<Response {ResponseStatusCode}>", response.StatusCode);
        await Sleep(500, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
            {
                LogFailedUrl(url);
                if (generatingManually)
                {
                    throw new WrongExtensionException();
                }

                if (SiteName != "pixiv")
                {
                    return false;
                }

                // TODO: Improve this
                // Api seems to always return .jpg even if the file is a .png or .gif
                var parts = fileLink.Url.Split(".");
                var ext = parts[^1];
                if (PixivExtMap.TryGetValue(ext, out var mappedExt))
                {
                    parts[^1] = mappedExt;
                    fileLink.Url = string.Join(".", parts);
                    Logger.Information("Trying again with .{MappedExt} extension...", mappedExt);
                }
                else
                {
                    Logger.Warning("Unable to download Pixiv image: {URL}", fileLink.Url);
                    skipDownload.Value = true;
                    return true; // Prevent further retries
                    // Some images may not exist, so we just log and move on
                    // e.g., https://www.pixiv.net/en/artworks/14742347
                    // A bit annoying since I am not sure if other extensions exist
                }

                return false;
            }
            case HttpStatusCode.Unauthorized:
                return false;
            case HttpStatusCode.Forbidden:
                switch (SiteName)
                {
                    case "kemono" when !url.Contains(".psd") && url.Contains("kemono"):
                        Logger.Information("Wrong subdomain, trying again...");
                        throw new BadSubdomainException();
                    case "e-hentai":
                        Logger.Information("E-Hentai URL expired, trying to update links...");
                        await Task.Delay(10 * MillisecondsInSecond,
                            cancellationToken); // Wait for 10 seconds before retrying
                        throw new UrlExpiredException(SiteName);
                }

                return false;
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.InternalServerError:
                return false;

            #region Unused Status Codes

            case HttpStatusCode.Continue:
            case HttpStatusCode.SwitchingProtocols:
            case HttpStatusCode.Processing:
            case HttpStatusCode.EarlyHints:
            case HttpStatusCode.OK:
            case HttpStatusCode.Created:
            case HttpStatusCode.Accepted:
            case HttpStatusCode.NonAuthoritativeInformation:
            case HttpStatusCode.NoContent:
            case HttpStatusCode.ResetContent:
            case HttpStatusCode.PartialContent:
            case HttpStatusCode.MultiStatus:
            case HttpStatusCode.AlreadyReported:
            case HttpStatusCode.IMUsed:
            case HttpStatusCode.Ambiguous:
            case HttpStatusCode.Moved:
            case HttpStatusCode.Found:
            case HttpStatusCode.RedirectMethod:
            case HttpStatusCode.NotModified:
            case HttpStatusCode.UseProxy:
            case HttpStatusCode.Unused:
            case HttpStatusCode.RedirectKeepVerb:
            case HttpStatusCode.PermanentRedirect:
            case HttpStatusCode.BadRequest:
            case HttpStatusCode.PaymentRequired:
            case HttpStatusCode.MethodNotAllowed:
            case HttpStatusCode.NotAcceptable:
            case HttpStatusCode.ProxyAuthenticationRequired:
            case HttpStatusCode.RequestTimeout:
            case HttpStatusCode.Conflict:
            case HttpStatusCode.Gone:
            case HttpStatusCode.LengthRequired:
            case HttpStatusCode.PreconditionFailed:
            case HttpStatusCode.RequestEntityTooLarge:
            case HttpStatusCode.RequestUriTooLong:
            case HttpStatusCode.UnsupportedMediaType:
            case HttpStatusCode.RequestedRangeNotSatisfiable:
            case HttpStatusCode.ExpectationFailed:
            case HttpStatusCode.MisdirectedRequest:
            case HttpStatusCode.UnprocessableEntity:
            case HttpStatusCode.Locked:
            case HttpStatusCode.FailedDependency:
            case HttpStatusCode.UpgradeRequired:
            case HttpStatusCode.PreconditionRequired:
            case HttpStatusCode.TooManyRequests:
            case HttpStatusCode.RequestHeaderFieldsTooLarge:
            case HttpStatusCode.UnavailableForLegalReasons:
            case HttpStatusCode.NotImplemented:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
            case HttpStatusCode.HttpVersionNotSupported:
            case HttpStatusCode.VariantAlsoNegotiates:
            case HttpStatusCode.InsufficientStorage:
            case HttpStatusCode.LoopDetected:
            case HttpStatusCode.NotExtended:
            case HttpStatusCode.NetworkAuthenticationRequired:
            default:
                Logger.Warning("Unhandled status code: {ResponseStatusCode}", response.StatusCode);
                return false;

            #endregion
        }
    }

    private async Task AssociateGoFileCookies(string url, CancellationToken cancellationToken = default)
    {
        Logger.Debug("Associating GoFile cookies");
        var siteLoginStatus = WebDriver.SiteLoginStatus;
        try
        {
            if (!siteLoginStatus.GetValueOrDefault("gofile", false))
            {
                Logger.Debug("Logging into GoFile");
                siteLoginStatus["gofile"] = await GoFileLogin(cancellationToken);
            }

            Driver.Url = url;
            Logger.Debug("Loading {CurrentUrl}", Driver.Url);
            Driver.Refresh();
            await Sleep(5000, cancellationToken);
            // TODO: Also need to get account token for requests
        }
        catch (WebDriverException)
        {
            // Ignore
            Logger.Warning("WebDriver unreachable, resetting...");
            WebDriver.RegenerateDriver();
        }
    }

    private async Task<bool> GoFileLogin(CancellationToken cancellationToken = default)
    {
        var origUrl = Driver.Url;
        var loginLink = Config.Custom.GoFile.LoginLink;
        Driver.Url = loginLink;
        await Sleep(10000, cancellationToken);
        for (var i = 0; i < RetryCount; i++)
        {
            await Sleep(2500, cancellationToken);
            if (Driver.Url == "https://gofile.io/myProfile")
            {
                Logger.Debug("Logged in to GoFile");
                break;
            }

            if (i == 3)
            {
                Logger.Warning("Failed to login to GoFile: {CurrentUrl}", Driver.Url);
                #if DEBUG
                Driver.TakeDebugScreenshot("gofile.png");
                #endif
            }
        }

        Driver.Url = origUrl;
        return true;
    }

    private async Task RenameFile(string src, string dst, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(dst))
        {
            File.Move(src, dst);
            return;
        }

        var srcHash = await FileUtility.GetFileHash(src, cancellationToken);
        var dstHash = await FileUtility.GetFileHash(dst, cancellationToken);
        if (srcHash.SequenceEqual(dstHash))
        {
            Logger.Information("File already exists and is same, deleting src...");
            File.Delete(src);
        }
        else
        {
            var ext = Path.GetExtension(dst);
            var filename = Path.GetFileNameWithoutExtension(dst);
            var directory = Path.GetDirectoryName(dst)!;
            var newFilename = $"{filename} ({DateTime.Now:yyyy-MM-dd HH-mm-ss}){ext}";
            File.Move(src, Path.Combine(directory, newFilename));
            Logger.Information("File already exists but is different, renaming src...");
        }
    }

    /// <summary>
    ///     Write response data to file
    /// </summary>
    /// <param name="response">Response to write to file</param>
    /// <param name="path">Filepath to write to</param>
    /// <param name="resumeFrom">Byte offset to resume from</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>Boolean based on successfulness</returns>
    private async Task<DownloadStatus> WriteToFile(HttpResponseMessage response, string path, long resumeFrom,
                                                   CancellationToken cancellationToken = default)
    {
        var expandedFilePath = path.StartsWith('~')
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..])
            : path;

        var idleTimeout = TimeSpan.FromSeconds(30); // 30 seconds idle timeout
        var savePath = Path.GetFullPath(expandedFilePath);
        try
        {
            return await BufferedWrite(response, savePath, idleTimeout, resumeFrom, cancellationToken);
        }
        catch (HttpRequestException)
        {
            Logger.Warning("Connection Reset, Retrying...");
            await Sleep(1000, cancellationToken); // Wait for 1 second before retrying
            return DownloadStatus.ConnectionReset;
        }
    }

    private async Task<DownloadStatus> BufferedWrite(HttpResponseMessage response, string savePath,
                                                     TimeSpan idleTimeout, long resumFrom,
                                                     CancellationToken cancellationToken = default)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream =
            new FileStream(savePath,
                resumFrom > 0 ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None);
        var buffer = new byte[4096]; // 4KB buffer
        var totalSize = 0L;
        var lastActivity = DateTime.UtcNow;

        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalSize += bytesRead;
                //Logger.Debug("Downloaded {TotalSize} bytes...", totalSize);
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                if (DateTime.UtcNow - lastActivity > idleTimeout)
                {
                    Logger.Warning("Download timed out due to inactivity.");
                    throw new DownloadTimeoutException(totalSize, "No data received for too long.");
                }

                lastActivity = DateTime.UtcNow;
            }
        }
        catch (IOException e)
        {
            if (e.Message.StartsWith("The response ended prematurely"))
            {
                Logger.Warning("Download response ended prematurely.");
                throw new DownloadTimeoutException(totalSize, e.Message, e);
            }

            if (e.Message.StartsWith("Received an unexpected EOF or 0 bytes from the transport stream"))
            {
                Logger.Warning("Received unexpected EOF from transport stream.");
                throw new DownloadTimeoutException(totalSize, e.Message, e);
            }

            Logger.Error("An IO error occured: {savePath} - Reason: {Reason}", savePath, e.Message);
            return DownloadStatus.Failed;
        }

        if (totalSize < MinimumFileSize)
        {
            Logger.Warning("Downloaded file is very small: {FilePath} ({Size} bytes)", savePath, totalSize);
            return SiteName == "e-hentai" ? throw new UrlExpiredException(SiteName) : DownloadStatus.Failed;
        }

        return DownloadStatus.Ok; // Success
    }

    /// <summary>
    ///     Recursively unzip all files in a given directory
    /// </summary>
    /// <param name="directoryPath">Path of directory to unzip files in</param>
    /// <param name="downloadStats">DownloadStats object to update with results</param>
    private void UnzipFiles(string directoryPath, DownloadStats downloadStats)
    {
        var count = 0;
        var error = 0;
        var files = Directory.GetFiles(directoryPath, "*.zip", SearchOption.AllDirectories);
        var (intermediateCount, intermediateError) = UncompressAndGetResults(files, UnzipFile);
        count += intermediateCount;
        error += intermediateError;

        files = Directory.GetFiles(directoryPath, "*.7z", SearchOption.AllDirectories);
        (intermediateCount, intermediateError) = UncompressAndGetResults(files, file =>
        {
            using var archive = SevenZipArchive.OpenArchive(file);
            UncompressFile(file, archive);
        });
        count += intermediateCount;
        error += intermediateError;

        files = Directory.GetFiles(directoryPath, "*.rar", SearchOption.AllDirectories);
        (intermediateCount, intermediateError) = UncompressAndGetResults(files, file =>
        {
            using var archive = RarArchive.OpenArchive(file);
            UncompressFile(file, archive);
        });
        count += intermediateCount;
        error += intermediateError;

        downloadStats.ArchivesExtracted += count;
        downloadStats.ArchivesExtractionFailed += error;
    }

    private (int, int) UncompressAndGetResults(string[] files, Action<string> uncompressAction)
    {
        var count = 0;
        var error = 0;
        foreach (var file in files)
        {
            try
            {
                uncompressAction(file);
                count++;
            }
            catch (Exception)
            {
                Logger.Error("Failed to extract: {file}", file);
                error++;
            }
        }

        return (count, error);
    }

    private static void UncompressFile(string archivePath, IArchive archive)
    {
        var extractPath = Path.ChangeExtension(archivePath, null);
        Directory.CreateDirectory(extractPath);
        // using var archive = SevenZipArchive.Open(archivePath);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            entry.WriteToDirectory(extractPath, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }
    }

    /// <summary>
    ///     Unzip a given file
    /// </summary>
    /// <param name="zipPath">Zip file to extract</param>
    private void UnzipFile(string zipPath)
    {
        var extractPath = Path.ChangeExtension(zipPath, null);
        Directory.CreateDirectory(extractPath);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, extractPath);
        }
        catch (InvalidDataException)
        {
            var ext = FileUtility.GetCorrectExtension(zipPath);
            var newPath = Path.ChangeExtension(zipPath, ext);
            File.Move(zipPath, newPath);
            return;
        }

        if (UnzipProtocol == UnzipProtocol.ExtractDelete)
        {
            File.Delete(zipPath);
        }
    }

    /// <summary>
    ///     Return the url without the filename attached
    /// </summary>
    /// <param name="url">Url to trim</param>
    /// <returns>The base url</returns>
    private static string TrimUrl(string url)
    {
        return url[..(url.LastIndexOf('/') + 1)];
    }

    private void AddCookies()
    {
        Logger.Error("Method not yet implemented...");
    }

    private bool CookiesNeeded()
    {
        return SiteName == "titsintops";
    }

    private static void LogFailedUrl(string url)
    {
        using var writer = new StreamWriter("failed.txt", true, Encoding.Unicode);
        writer.WriteLine(url);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Session.Dispose();
        DriverPool.ReleaseDriver(WebDriver);
        GC.SuppressFinalize(this);
    }

    ~ImageRipper()
    {
        Dispose();
    }

    [GeneratedRegex(@"<img.+src=""([^""]+)""")]
    private static partial Regex NLegsImageUrlRegex();
}