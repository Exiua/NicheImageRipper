using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using ImageMagick;
using IwaraApiClient;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
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
using SteamKit2;
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

    private bool _disposed;

    private FirefoxDriver Driver => WebDriver.Driver;

    private static GeneralConfig Config => Configuration.Config.Instance;
    private static TokenManager TokenManager => TokenManager.Instance;
    private static FlareSolverrManager FlareSolverrManager => NicheImageRipper.FlareSolverrManager;

    public delegate void ProgressChangedHandler(int current, int total);

    public event ProgressChangedHandler? OnProgressChanged;

    public ImageRipper(WebDriverPool driverPool, FilenameScheme filenameScheme = FilenameScheme.Original,
                       UnzipProtocol unzipProtocol = UnzipProtocol.None,
                       PostDownloadAction postDownloadAction = PostDownloadAction.None)
    {
        FilenameScheme = filenameScheme;
        UnzipProtocol = unzipProtocol;
        PostDownloadAction = postDownloadAction;
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
    }

    public async Task Rip(string url, CancellationToken cancellationToken = default)
    {
        // Cannot rip if the instance is disposed, but everything else is fine to access
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ImageRipper), "Cannot use a disposed ImageRipper instance");
        }

        OnProgressChanged?.Invoke(0, 0); // Indeterminate progress at the start
        SleepTime = 0.2f; // Reset sleep time
        GivenUrl = url.Replace("members.", "www."); // Replace is done to properly parse hanime pages
        (SiteName, SleepTime) = UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        Logger.Debug("Site Name: {SiteName}", SiteName);
        Logger.Debug("Checking if cookies are needed for {SiteName}", SiteName);
        if (CookiesNeeded())
        {
            Logger.Debug("Adding cookies needed for {SiteName}", SiteName);
            AddCookies();
        }

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

    private async Task FileGetter(CancellationToken cancellationToken = default)
    {
        LoadCorrectWebDriver();

        var htmlParser = HtmlParser.GetParser(SiteName, WebDriver, ClientManager, RequestHeaders, FilenameScheme);
        Logger.Debug("Constructed HtmlParser");
        FolderInfo = await htmlParser.ParseSite(GivenUrl, cancellationToken);
        //Logger.Debug("Folder Info: {@FolderInfo}", FolderInfo);
        //Logger.Debug("Directory Name: {DirectoryName}", FolderInfo.DirectoryName);
        var fullPath = Path.Combine(SavePath, FolderInfo.DirectoryName);
        if (Interrupted && FilenameScheme != FilenameScheme.Hash)
        {
            // TODO: self.folder_info.urls = self.get_incomplete_files(full_path)
        }

        Logger.Debug("Dir Length: {DirLength}", fullPath.Length);
        Directory.CreateDirectory(fullPath);

        var start = await GetStartIndex(cancellationToken);
        DownloadStats downloadStats;
        IndexedHashes filesHashes;
        if (!File.Exists(RipStatePath))
        {
            downloadStats = new DownloadStats();
            filesHashes = new IndexedHashes();
        }
        else
        {
            var state = JsonUtility.Deserialize<RipState>(RipStatePath);
            if (state is null)
            {
                Logger.Error("Failed to load rip state");
                throw new RipperException("Failed to load rip state");
            }

            downloadStats = state.DownloadStats;
            filesHashes = IndexedHashes.Deserialize(state.FilesHashes);
            Logger.Debug("Truncating files hashes to index {StartIndex}", start);
            filesHashes.TruncateToIndex(start);
        }

        try
        {
            // Can get the image through numerically ascending url for imhentai and hentairox
            //   (hard to account for GIFs and other extensions otherwise)
            if (FolderInfo.MustGenerateManually)
            {
                await HandleGeneratingManually(start, fullPath, filesHashes, downloadStats, cancellationToken);
            }
            // Easier to put all image url in a list and then download for these sites
            else
            {
                await HandleDownloadingFromList(start, fullPath, filesHashes, downloadStats, cancellationToken);
            }
        }
        catch (Exception e)
        {
            Logger.Debug("Saving rip state due to exception. Reason: {Message}", e.Message);
            var state = new RipState
            {
                DownloadStats = downloadStats,
                FilesHashes = filesHashes.Serialize(),
            };

            JsonUtility.Serialize(RipStatePath, state);
            Logger.Debug("Saved rip state to {RipStatePath}", RipStatePath);
            throw;
        }

        File.Delete(RipIndexPath);
        File.Delete(RipStatePath); // Existence of rip state file indicates incomplete rip
        if (((double)downloadStats.FailedDownloadsCount) / FolderInfo.NumUrls > FailureThreshold)
        {
            var msg = $"More than {FailureThreshold * 100}% of the images failed to download";
            var e = new RipperException(msg);
            Logger.Error(e, "{msg:l}", msg);
            if (SiteName == "sexbjcam")
            {
                File.Delete("partial.json");
            }

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
        OnProgressChanged?.Invoke(1, 1); // Complete progress at the end
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

    private async Task HandleDownloadingFromList(int start, string fullPath, IndexedHashes filesHashes,
                                                 DownloadStats downloadStats,
                                                 CancellationToken cancellationToken = default)
    {
        switch (SiteName)
        {
            case "deviantart":
                // Delegated to external tool
                var success = await DeviantArtDownload(fullPath, FolderInfo.Urls[0].Url, cancellationToken);
                if (!success)
                {
                    // TODO
                }
                
                break;
            // Probably need to extract parts into separate methods
            default:
            {
                var completed = new bool[FolderInfo.NumUrls];
                // Loop needed to retry current index after refreshing EHentai links once they expire
                while (true)
                {
                    try
                    {
                        if (SiteName == "archivebate")
                        {
                            var total = FolderInfo.NumUrls;
                            var complete = 0;
                            OnProgressChanged?.Invoke(complete + 1, total + 1);
                            const int maxAttempts = 4;
                            const int maxConcurrentConnections = 30;
                            var semaphore = new SemaphoreSlim(maxConcurrentConnections, maxConcurrentConnections);
                            var tasks = FolderInfo.Urls.Select(async (link, i) =>
                            {
                                await semaphore.WaitAsync(cancellationToken);
                                if (completed[i])
                                {
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
                                        await DownloadSingleFromList(i, link, fullPath, filesHashes, downloadStats, cancellationToken: cancellationToken);
                                        Interlocked.Increment(ref complete);
                                        completed[i] = true;
                                        Logger.Information("Finished downloading {Index}, {Total} remaining", i + 1,
                                            total - complete);
                                        OnProgressChanged?.Invoke(complete + 1, total + 1);
                                        break;
                                    }
                                    catch (Exception e)
                                    {
                                        if (attempt == maxAttempts - 1)
                                        {
                                            Logger.Error(e, "Error downloading {Index}, {Total} remaining: {Url}",
                                                i + 1,
                                                total - complete, link.Url);
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
                        else
                        {
                            foreach (var (i, link) in FolderInfo.Urls.Skip(start).Enumerate())
                            {
                                while (Paused)
                                {
                                    await Sleep(1000, cancellationToken);
                                }

                                // Compute the absolute index (i is the relative index after start)
                                var index = start + i;
                                await DownloadSingleFromList(index, link, fullPath, filesHashes, downloadStats, true, cancellationToken);
                            }
                        }

                        break;
                    }
                    catch (EHentaiUrlExpiredException e)
                    {
                        Logger.Information("Refreshing EHentai links");
                        Logger.Debug("Start index for refresh: {StartIndex}", e.ResumeIndex);
                        var parser = new EHentaiParser(WebDriver, ClientManager, RequestHeaders, FilenameScheme);
                        start = e.ResumeIndex;
                        var updatedLinks = await parser.UpdateLinks(FolderInfo.Urls, start);
                        FolderInfo.Urls = updatedLinks;
                    }
                    catch (PornhubUrlExpiredException e)
                    {
                        Logger.Information("Refreshing Pornhub links");
                        Logger.Debug("Start index for refresh: {StartIndex}", e.ResumeIndex);
                        var parser = new PornhubParser(WebDriver, ClientManager, RequestHeaders, FilenameScheme);
                        start = e.ResumeIndex;
                        var updatedLinks = await parser.UpdateLinks(FolderInfo.Urls, start);
                        FolderInfo.Urls = updatedLinks;
                    }
                }

                break;
            }
        }
    }

    private async Task DownloadSingleFromList(int index, ImageLink link, string fullPath, IndexedHashes filesHashes,
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
            var skipDownload = new Box<bool>(false);
            var success = await DownloadFromList(link, imagePath, index, downloadStats, skipDownload, cancellationToken);
            if (success && !skipDownload)
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
        catch (EHentaiUrlExpiredException e)
        {
            Logger.Debug("Caught EHentaiUrlExpiredException, need to refresh links");
            e.ResumeIndex = index;
            throw;
        }
        catch (PornhubUrlExpiredException e)
        {
            Logger.Debug("Caught PornhubUrlExpiredException, need to refresh links");
            e.ResumeIndex = index;
            throw;
        }
        catch (Exception e)
        {
            if (e.Message.Contains("see inner exception"))
            {
                Logger.Debug("Caught exception with inner exception while downloading {Url}: {InnerException}",
                    link.Url,
                    e.InnerException?.Message);
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

    private async Task PostProcess(ImageLink link, string imagePath, IndexedHashes filesHashes,
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

            if (link.LinkInfo is LinkInfo.Mega or LinkInfo.YoutubeVideo or LinkInfo.SteamCommunity)
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

    private async Task<bool> DeviantArtDownload(string fullPath, string url,
                                                CancellationToken cancellationToken = default)
    {
        var cmd = new[]
        {
            "-D", $"\"{fullPath}\"", "-u", Config.Logins.DeviantArt.Username, "-p",
            Config.Logins.DeviantArt.Password, "--write-log", "log.txt", url
        };
        var (exitCode, _, _) = await RunSubprocess("gallery-dl", cmd, startMessage: "Starting Deviantart download",
            endMessage: "Deviantart download finished", cancellationToken: cancellationToken);
        if (exitCode != 0)
        {
            Logger.Error("Failed to download from DeviantArt");
        }

        return exitCode == 0;
    }

    internal static async Task<FfmpegStatusCode> RunFfmpeg(string[] cmd,
                                                           string startMessage = "Starting ffmpeg download",
                                                           string endMessage = "Ffmpeg download finished",
                                                           bool displayOutput = false,
                                                           CancellationToken cancellationToken = default)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.Ffmpeg))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.Ffmpeg);
        }

        if (!displayOutput)
        {
            cmd = ["-loglevel", "quiet", "-y", ..cmd];
        }
        else
        {
            cmd = ["-y", ..cmd];
        }

        Log.Debug("ffmpeg {cmd}", string.Join(" ", cmd));
        var (exitCode, _, _) = await RunSubprocess("ffmpeg", cmd, captureError: displayOutput,
            startMessage: startMessage, endMessage: endMessage, cancellationToken: cancellationToken);
        if (exitCode != 0)
        {
            Log.Error("Failed to run ffmpeg: {ExitCode}", exitCode);
        }

        return (FfmpegStatusCode)exitCode;
    }

    private async Task<bool> RunYtDlp(ImageLink link, string path, string startMessage, string endMessage,
                                      CancellationToken cancellationToken = default)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.YtDlp))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.YtDlp);
        }

        var url = link.Url;
        var parent = Directory.GetParent(path)!.FullName;
        var filename = Path.GetFileName(path);
        string[] cmd = link.Referer != ""
            ?
            [
                "--force-overwrites",
                "-P", $"\"{parent}\"",
                "-o", $"\"{filename}\"",
                "--add-headers", $"\"Referer:{link.Referer}\"",
                $"\"{url}\"",
            ]
            :
            [
                "--force-overwrites",
                "-P", $"\"{parent}\"",
                "-o", $"\"{filename}\"",
                $"\"{url}\"",
            ];
        //cmd = [ "--no-warnings", ..cmd ];
        Logger.Debug("yt-dlp {cmd}", string.Join(" ", cmd));
        var (exitCode, output, _) = await RunSubprocess("yt-dlp", cmd, true, /* true,*/
            startMessage: startMessage, endMessage: endMessage, cancellationToken: cancellationToken);

        if (exitCode == 0 || link.LinkInfo != LinkInfo.YoutubeVideo)
        {
            return exitCode == 0;
        }

        var lines = output!.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ageRestricted = lines.Any(line => line.Contains("This video is age-restricted"));
        if (ageRestricted)
        {
            if (!File.Exists(YoutubeCookiesFile))
            {
                Logger.Error("Video is age-restricted but no cookies file found at {YoutubeCookiesFile}",
                    YoutubeCookiesFile);
                return false;
            }

            Logger.Information("Video is age-restricted, trying again with cookies");
            cmd =
            [
                "--force-overwrites",
                "--cookies", $"\"{YoutubeCookiesFile}\"",
                "-P", $"\"{parent}\"",
                "-o", $"\"{filename}\"",
                $"\"{url}\"",
            ];

            (exitCode, _, _) = await RunSubprocess("yt-dlp", cmd, true, /* true,*/
                startMessage: startMessage, endMessage: endMessage, cancellationToken: cancellationToken);
        }

        Logger.Error("Failed to run yt-dlp: {ExitCode}", exitCode);

        return exitCode == 0;
    }

    private static async Task<(int, string?, string?)> RunSubprocess(string executable, string[]? arguments = null,
                                                                     bool captureOutput = false,
                                                                     bool captureError = false,
                                                                     string? startMessage = null,
                                                                     string? endMessage = null,
                                                                     CancellationToken cancellationToken = default)
    {
        if (startMessage is not null)
        {
            Log.Information("{StartMessage:l}", startMessage);
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments is null ? "" : " ".Join(arguments),
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureError,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        StringBuilder? output = null;
        if (captureOutput)
        {
            output = new StringBuilder();
            process.OutputDataReceived += (_, args) =>
            {
                var data = args.Data ?? "null";
                output.AppendLine(data);
                Log.Debug("{Data:l}", data);
            };
        }

        StringBuilder? error = null;
        if (captureError)
        {
            error = new StringBuilder();
            process.ErrorDataReceived += (_, args) =>
            {
                var data = args.Data ?? "null";
                error.AppendLine(data);
                Log.Debug("{Data:l}", data);
            };
        }

        process.Start();

        if (captureOutput)
        {
            process.BeginOutputReadLine();
        }

        if (captureError)
        {
            process.BeginErrorReadLine();
        }

        await process.WaitForExitAsync(cancellationToken);
        var exitCode = process.ExitCode;
        if (endMessage is not null)
        {
            Log.Information("{EndMessage:l}", endMessage);
        }

        var outputStr = output?.ToString();
        var errorStr = error?.ToString();

        return (exitCode, outputStr, errorStr);
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
    /// <param name="imageLink">ImageLink containing data on the file to download</param>
    /// <param name="filename">Name of the file to download</param>
    /// <param name="imagePath">Full path to download the file to</param>
    /// <param name="ext">Extension of the file to download</param>
    /// <param name="skipDownload"></param>
    /// <param name="cancellationToken">Cancellation token to cancel the download operation</param>
    private async Task<bool> DownloadFromUrl(ImageLink imageLink, string filename, string imagePath, string ext,
                                             Box<bool> skipDownload, CancellationToken cancellationToken = default)
    {
        var numFiles = FolderInfo.NumUrls;
        // Completes the specific image URL from the general URL
        var url = imageLink.Url;
        var fullFilename = $"{filename}{ext}";
        var ripUrl = $"{url}{fullFilename}";
        var numProgress = $"({filename}/{numFiles})";
        Logger.Information("{RipUrl:l}    {NumProgress:l}", ripUrl, numProgress);
        imageLink.Url = ripUrl;
        bool success;
        try
        {
            success = await DownloadFile(imagePath, imageLink, true, skipDownload, cancellationToken);
        }
        finally
        {
            imageLink.Url = url;
        }

        await Sleep(50, cancellationToken);
        return success;
    }

    /// <summary>
    ///     Download images from url supplied from a list of image urls
    /// </summary>
    /// <param name="imageLink">ImageLink containing data on the file to download</param>
    /// <param name="imagePath">Full path of the location to save the file to</param>
    /// <param name="currentFileNum">Number of the file being downloaded</param>
    /// <param name="downloadStats">DownloadStats object to update with results</param>
    /// <param name="skipDownload"></param>
    /// <param name="cancellationToken">Cancellation token to cancel the download operation</param>
    private async Task<bool> DownloadFromList(ImageLink imageLink, string imagePath, int currentFileNum,
                                              DownloadStats downloadStats, Box<bool> skipDownload,
                                              CancellationToken cancellationToken = default)
    {
        var numFiles = FolderInfo.NumUrls;
        var ripUrl = imageLink.Url;
        var displayUrl = imageLink.LinkInfo == LinkInfo.Base64 ? UrlUtility.TruncateLongUrl(ripUrl) : ripUrl;
        Logger.Information("{Url:l}    ({CurrentProgress}/{TotalProgress})", displayUrl, currentFileNum + 1, numFiles);
        var oldReferer = RequestHeaders[RequestHeaderKeys.Referer];
        if (imageLink.HasReferer)
        {
            RequestHeaders[RequestHeaderKeys.Referer] = imageLink.Referer;
        }
        else if (imageLink.Referer is null)
        {
            RequestHeaders[RequestHeaderKeys.Referer] = "";
        }

        bool success;
        switch (imageLink.LinkInfo)
        {
            case LinkInfo.M3U8Ffmpeg:
                success = await DownloadM3U8ToMp4(imagePath, imageLink, cancellationToken);
                if (!success)
                {
                    success = await DownloadObfuscatedM3U8(imagePath, imageLink, cancellationToken);
                }

                break;
            case LinkInfo.M3U8YtDlp:
                success = await DownloadM3U8YtDlp(imagePath, imageLink, cancellationToken);
                if (!success)
                {
                    success = await DownloadObfuscatedM3U8(imagePath, imageLink, cancellationToken);
                }

                break;
            case LinkInfo.ObfuscatedM3U8:
                success = await DownloadObfuscatedM3U8(imagePath, imageLink, cancellationToken);
                break;
            case LinkInfo.GDrive:
                success = await DownloadGDriveFile(imagePath, imageLink, cancellationToken);
                break;
            case LinkInfo.IframeMedia:
                success = await DownloadIframeMedia(imagePath, imageLink, cancellationToken);
                // TODO: Figure out how to delete temp directories
                break;
            case LinkInfo.Mega:
                success = await DownloadMegaFiles(imagePath, imageLink, cancellationToken);
                Logger.Debug("Success from Mega: {Success}", success);
                break;
            case LinkInfo.PixelDrain:
                success = await DownloadPixelDrainFiles(imagePath, imageLink, cancellationToken);
                break;
            case LinkInfo.YoutubeVideo:
                success = await DownloadYoutubeVideo(imagePath, imageLink, cancellationToken);
                await Sleep(1250, cancellationToken);
                break;
            case LinkInfo.Text:
                await File.AppendAllTextAsync(imagePath, ripUrl + "\n", cancellationToken);
                success = true;
                break;
            case LinkInfo.MpegDash:
                success = await DownloadMpegDashFile(imagePath, imageLink, cancellationToken);
                break;
            case LinkInfo.ResolveImage:
                success = await ResolveAndDownloadFile(imagePath, imageLink, skipDownload, cancellationToken);
                break;
            case LinkInfo.SeleniumImage:
                success = await DownloadSeleniumImage(imagePath, imageLink, cancellationToken);
                break;
            case LinkInfo.Base64:
                success = await DownloadBase64Image(imagePath, imageLink, cancellationToken);
                break;
            case LinkInfo.PixivUgoira:
                success = await DownloadPixivUgoira(imagePath, imageLink, cancellationToken);
                break;
            case LinkInfo.SteamCommunity:
                success = await DownloadSteamCommunity(imagePath, imageLink, cancellationToken);
                break;
            case LinkInfo.Iwara:
                success = await DownloadIwara(imagePath, imageLink, cancellationToken);
                break;
            case LinkInfo.GoFile:
            case LinkInfo.None:
                success = await DownloadFile(imagePath, imageLink, false, skipDownload, cancellationToken);
                break;
            default:
                var e = new RipperException("Unknown LinkInfo: " + imageLink.LinkInfo);
                Logger.Error(e, "Unknown LinkInfo: {LinkInfo}", imageLink.LinkInfo);
                throw e;
        }

        if (!success)
        {
            if (Config.SkipFailedDownloads)
            {
                downloadStats.FailedDownloads.Add(ripUrl);
            }
            else
            {
                throw new RipperException("Failed to download file: " + ripUrl);
            }
        }

        RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
        await Sleep(50, cancellationToken);

        return success;
    }

    private async Task<bool> ResolveAndDownloadFile(string path, ImageLink imageLink, Box<bool> skipDownload,
                                                    CancellationToken cancellationToken = default)
    {
        var url = imageLink.Url;
        for (var i = 0; i < RetryCount; i++)
        {
            var imageUrl = await GetDownloadUrl(url, cancellationToken);
            if (imageUrl == "")
            {
                await Sleep(500, cancellationToken);
                continue;
            }

            Logger.Debug("Resolved URL: {Url}", imageUrl);
            imageLink.Url = imageUrl;

            var success = await DownloadFile(path, imageLink, false, skipDownload, cancellationToken);
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

    private async Task<bool> DownloadMpegDashFile(string filePath, ImageLink imageLink,
                                                  CancellationToken cancellationToken = default)
    {
        var parent = Directory.GetParent(filePath)!.FullName;
        var filename = Path.GetFileName(filePath);
        var cmd = new[] { "-P", $"\"{parent}\"", imageLink.Url, "-o", filename };
        var (exitCode, _, _) = await RunSubprocess("yt-dlp", cmd, startMessage: "Starting youtube-dl download",
            endMessage: "youtube-dl download finished", cancellationToken: cancellationToken);
        return exitCode == 0;
    }

    private async Task<bool> DownloadM3U8ToMp4(string filePath, ImageLink imageLink,
                                               CancellationToken cancellationToken = default)
    {
        var url = imageLink.Url;
        var referer = imageLink.Referer;
        if (!filePath.Contains('.'))
        {
            if (url.Contains(".mp4"))
            {
                filePath += ".mp4";
            }
            else if (url.Contains(".webm"))
            {
                filePath += ".webm";
            }
            else
            {
                filePath += ".ts";
            }
        }

        string[] cmd;
        if (referer != "")
        {
            cmd =
            [
                "-headers",
                $"\"Referer: {referer}\"",
                "-headers",
                $"\"User-Agent: {Config.UserAgent}\"",
                "-protocol_whitelist", "file,http,https,tcp,tls,crypto",
                "-i", $"\"{url}\"",
                "-c", "copy",
                $"\"{filePath}\""
            ];
        }
        else
        {
            cmd =
            [
                "-protocol_whitelist", "file,http,https,tcp,tls,crypto",
                "-i", $"\"{url}\"",
                "-c", "copy",
                $"\"{filePath}\""
            ];
        }

        var result = await RunFfmpeg(cmd, cancellationToken: cancellationToken);
        Logger.Debug("Ffmpeg result: {Result}", result.GetShortErrorMessage());
        return result.IsSuccess();
    }

    private Task<bool> DownloadM3U8YtDlp(string filePath, ImageLink imageLink,
                                         CancellationToken cancellationToken = default)
    {
        return RunYtDlp(imageLink, filePath, startMessage: "Starting yt-dlp download",
            endMessage: "yt-dlp download finished", cancellationToken: cancellationToken);
    }

    private static async Task<bool> DownloadGDriveFile(string filePath, ImageLink imageLink,
                                                       CancellationToken cancellationToken = default)
    {
        var destinationPath = Path.Combine(filePath, imageLink.Filename);
        var parent = Directory.GetParent(destinationPath)!.FullName;
        Directory.CreateDirectory(parent);
        var credentials = await TokenManager.GDriveAuthenticate();
        var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credentials,
            ApplicationName = "ImageRipper"
        });
        var request = service.Files.Get(imageLink.Url);
        await using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
        await request.DownloadAsync(stream, cancellationToken);
        return true;
    }

    private static async Task<bool> DownloadIframeMedia(string filePath, ImageLink imageLink,
                                                        CancellationToken cancellationToken = default)
    {
        var parentPathInfo = Directory.GetParent(filePath)!;
        var parentPath = parentPathInfo.FullName;
        Directory.CreateDirectory(parentPath);
        for (var i = 0; i < RetryCount; i++)
        {
            try
            {
                var video = new BunnyVideoDrm(
                    referer: imageLink.Url,
                    embedUrl: imageLink.Referer!,
                    name: Path.GetFileName(filePath).Split('.')[0],
                    path: parentPath
                );
                await video.Download(cancellationToken);
                foreach (var f in parentPathInfo.EnumerateFiles(".*"))
                {
                    f.Delete();
                }

                break;
            }
            catch (UnauthorizedAccessException) // except (yt_dlp.utils.DownloadError, PermissionError)
            {
                if (i == 3)
                {
                    LogFailedUrl(imageLink.Url);
                    return false;
                }
            }
        }

        return true;
    }

    private async Task<bool> DownloadMegaFiles(string filePath, ImageLink imageLink,
                                               CancellationToken cancellationToken = default)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.MegaCmd))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.MegaCmd);
        }

        Logger.Debug("Logging in to MegaCmd");
        var (email, password) = Config.Logins.Mega;
        if (!PersistentLogins.TryGetValue("Mega", out var loggedIn))
        {
            PersistentLogins["Mega"] = MegaApi.WhoAmI() == email || MegaApi.Login(email, password);
        }
        else
        {
            if (!loggedIn)
            {
                PersistentLogins["Mega"] = MegaApi.WhoAmI() == email || MegaApi.Login(email, password);
            }
        }

        if (!PersistentLogins["Mega"])
        {
            var e = new RipperException("Unable to login to MegaCmd");
            Logger.Error(e, "Unable to login to MegaCmd");
            throw e;
        }

        if (imageLink.Url.Contains("/file/"))
        {
            Logger.Debug("Downloading file from Mega: {Url}", imageLink.Url);
            filePath = Path.GetDirectoryName(filePath)!;
        }
        else
        {
            Logger.Debug("Downloading folder from Mega: {Url}", imageLink.Url);
            Directory.CreateDirectory(filePath);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // TODO: Need better way to check if megacmd has timeout or is just downloading large amounts of data
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(60));
            try
            {
                return await MegaApi.DownloadAsync(imageLink.Url, filePath, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Logger.Warning("Mega download timed out, retrying...");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to download from Mega: {Url}", imageLink.Url);
                if (e.Message.Contains("No such file or directory"))
                {
                    Logger.Error("The specified file or directory does not exist on Mega: {Url}", imageLink.Url);
                    return false;
                }

                if (e.Message.Contains("Invalid URL"))
                {
                    Logger.Error("The provided URL is invalid: {Url}", imageLink.Url);
                    return false;
                }

                throw; // Re-throw the exception for further handling
            }
        }
    }

    private async Task<bool> DownloadPixelDrainFiles(string filePath, ImageLink imageLink,
                                                     CancellationToken cancellationToken = default)
    {
        var apiKey = Config.Keys.Pixeldrain;
        var authString = $":{apiKey}";
        var base64Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
        var headers = new Dictionary<string, string>
        {
            [RequestHeaderKeys.UserAgent] = Config.UserAgent,
            [RequestHeaderKeys.Authorization] = $"Basic {base64Auth}"
        };
        using var client = new HttpClient();
        foreach (var (key, value) in headers)
        {
            client.DefaultRequestHeaders.Add(key, value);
        }

        var response = await client.GetAsync($"https://pixeldrain.com/api/file/{imageLink.Url}",
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fileStream, cancellationToken);
        return true;
    }

    private Task<bool> DownloadYoutubeVideo(string filePath, ImageLink imageLink,
                                            CancellationToken cancellationToken = default)
    {
        return RunYtDlp(imageLink, filePath, startMessage: "Starting youtube-dl download",
            endMessage: "youtube-dl download finished", cancellationToken: cancellationToken);
    }

    private async Task<bool> DownloadSeleniumImage(string filePath, ImageLink imageLink,
                                                   CancellationToken cancellationToken = default)
    {
        try
        {
            var imageData = GetImageViaSelenium(imageLink.Url);
            await File.WriteAllBytesAsync(filePath, imageData, cancellationToken);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to download image");
            return false;
        }
    }

    private async Task<bool> DownloadBase64Image(string filePath, ImageLink imageLink,
                                                 CancellationToken cancellationToken = default)
    {
        try
        {
            var base64Data = imageLink.Url.Split(',')[1];
            var imageData = Convert.FromBase64String(base64Data);
            await File.WriteAllBytesAsync(filePath, imageData, cancellationToken);
            return true;
        }
        catch (FormatException e)
        {
            Logger.Error(e, "Failed to decode base64 image");
            return false;
        }
    }

    private async Task<bool> DownloadObfuscatedM3U8(string filePath, ImageLink imageLink,
                                                    CancellationToken cancellationToken = default)
    {
        try
        {
            var parent = Directory.GetParent(filePath)!.FullName;
            var referer = imageLink.Referer == "" ? null : imageLink.Referer;
            var ext = Path.GetExtension(filePath);
            if (ext == "")
            {
                imageLink.Filename += ".mp4";
            }

            await M3U8Downloader.DownloadM3U8(imageLink.Url, parent, imageLink.Filename, referer,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (HttpRequestException e)
        {
            if (SiteName == "pornhub" && e.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                //Logger.Debug("Caught HttpRequestException with 410 Gone status code from Pornhub, need to refresh links");
                throw new PornhubUrlExpiredException();
            }

            Logger.Error(e, "Failed to download obfuscated M3U8");
            return false;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to download obfuscated M3U8");
            return false;
        }
    }

    private async Task<bool> DownloadPixivUgoira(string filePath, ImageLink imageLink,
                                                 CancellationToken cancellationToken = default)
    {
        var illustId = imageLink.Url.Split("/")[4];
        var metadataUrl = $"https://www.pixiv.net/ajax/illust/{illustId}/ugoira_meta";
        Logger.Debug("Fetching Pixiv Ugoira metadata from {MetadataUrl}", metadataUrl);
        // Should contain PHPSESSID (checked in PixivParser)
        var sessionId =
            TokenManager.GetTokenWithRotation(RotationKey.Pixiv, TimeSpan.FromHours(24), Config.Cookies.Pixiv);
        Driver.Url = "https://www.pixiv.net/";
        Driver.SetCookie("PHPSESSID", sessionId);
        Driver.Url = metadataUrl;
        var rawView = Driver.FindElement(By.Id("rawdata-tab"));
        rawView.Click();
        var jsonPre = Driver.FindElement(By.XPath("//pre[@class='data']"));
        var rawJson = jsonPre.Text;
        var json = JsonNode.Parse(rawJson);
        if (json is null)
        {
            throw new RipperException("Failed to parse Pixiv Ugoira metadata");
        }

        json = json.AsObject();
        if (json["error"].Deserialize<bool>())
        {
            throw new RipperException("Received error while fetching Pixiv Ugoira metadata");
        }

        var oldReferer = RequestHeaders[RequestHeaderKeys.Referer];
        RequestHeaders[RequestHeaderKeys.Referer] = $"https://www.pixiv.net/artworks/{illustId}";
        var body = json["body"]!.AsObject();
        var keys = new[] { "originalSrc", "src" };
        HttpResponseMessage response = null!; // Must be assigned before exiting loop
        List<(string, int)> framesMetadata = null!;
        foreach (var (i, key) in keys.Enumerate())
        {
            var src = body[key]?.GetValue<string>();
            if (src is null)
            {
                Logger.Warning("Pixiv Ugoira source not found for key: {Key}", key);
                if (i == keys.Length - 1)
                {
                    RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
                    throw new RipperException("Pixiv Ugoira source not found");
                }

                continue;
            }

            var request = RequestHeaders.ToRequest(HttpMethod.Head, src);
            response = await Session.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("Failed to access Pixiv Ugoira source: {Src}", src);
                if (i == keys.Length - 1)
                {
                    RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
                    throw new RipperException("Unable to access Pixiv Ugoira source");
                }

                continue;
            }

            request = RequestHeaders.ToRequest(HttpMethod.Get, src);
            response = await Session.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("Failed to download Pixiv Ugoira: {Src}", src);
                if (i == keys.Length - 1)
                {
                    RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
                    throw new RipperException("Unable to download Pixiv Ugoira");
                }

                continue;
            }

            framesMetadata = body["frames"]!
                            .AsArray()
                            .Select(f => (f!["file"]!.GetValue<string>(), f["delay"]!.GetValue<int>()))
                            .OrderBy(f => f.Item1)
                            .ToList();
            break;
        }

        await using var zipStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        using var animation = new MagickImageCollection();
        foreach (var (fileName, delay) in framesMetadata)
        {
            var entry = archive.GetEntry(fileName);
            if (entry is null)
            {
                Logger.Warning("Warning: {FileName} not found in ZIP", fileName);
                continue;
            }

            await using var entryStream = await entry.OpenAsync(cancellationToken);
            var img = new MagickImage(entryStream)
            {
                AnimationDelay = (uint)(delay / 10) // Convert milliseconds to centiseconds
            };
            animation.Add(img);
        }

        animation[0].AnimationIterations = 0;
        //animation.OptimizeTransparency();
        await animation.WriteAsync(filePath, cancellationToken);
        RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
        TokenManager.UpdateTokenRotation(RotationKey.Pixiv);
        return true;
    }

    private async Task<bool> DownloadSteamCommunity(string filePath, ImageLink imageLink,
                                                    CancellationToken cancellationToken = default)
    {
        var destinationFolder = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(destinationFolder);
        var url = imageLink.Url;
        var ids = url.Split('/')[^1].Split('|');
        //var appId = ids[0];
        var fileId = ids[1];
        var (username, password) = Config.Logins.SteamCommunity;

        var client = ClientManager.SteamApiClient;
        try
        {
            await client.LoginAsync(username, password, cancellationToken);
            await client.DownloadWorkshopFileAsync(ulong.Parse(fileId), destinationFolder, cancellationToken);
        }
        catch (SteamKitWebRequestException e) when (e.Message.Contains("503"))
        {
            Logger.Warning(e, "Steam Community is currently unavailable (503), retrying...");
            await Sleep(2500, cancellationToken);
            return false;
        }
        catch (AsyncJobFailedException e)
        {
            Logger.Warning(e, "Failed to download Steam Community file, retrying...");
            await client.LogoutAsync(cancellationToken);
            return false;
        }
        catch (HttpRequestException e) when (e.InnerException is IOException { InnerException: SocketException } ex)
        {
            Logger.Warning(ex, "Network error while trying to download Steam Community: {Url}", url);
            await client.LogoutAsync(cancellationToken);
            return false;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to download Steam Community file");
            return false;
        }

        return true;
    }

    private async Task<bool> DownloadIwara(string filePath, ImageLink imageLink,
                                           CancellationToken cancellationToken = default)
    {
        var client = ClientManager.IwaraClient;
        var url = imageLink.Url;
        var videoId = url.Split('/')[^1];
        var success = await client.DownloadVideo(videoId, filePath, cancellationToken);
        await HtmlParser.JitterSleep(cancellationToken: cancellationToken);
        return success;
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

    /*private static string ExtractDownloadPath(string line)
    {
        var inPath = false;
        var path = "";
        var found = false;
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var c in line)
        {
            if (found)
            {
                break;
            }

            switch (c)
            {
                case '"':
                    if (!inPath)
                    {
                        inPath = true;
                    }
                    else
                    {
                        found = true;
                    }

                    break;
                default:
                    if (inPath)
                    {
                        path += c;
                    }

                    break;
            }
        }

        return path;
    }*/

    private async Task<bool> DownloadFile(string filePath, ImageLink imageLink, bool generatingManually,
                                          Box<bool> skipDownload, CancellationToken cancellationToken = default)
    {
        if (filePath[^1] == '/')
        {
            filePath = filePath[..^1];
        }

        var success = false;
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            success = await DownloadFileHelper(imageLink, filePath, generatingManually, skipDownload,
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
            imageLink.Filename = newFilename;
        }

        return true;
    }

    private async Task<bool> DownloadFileHelper(ImageLink imageLink, string imagePath, bool generatingManually,
                                                Box<bool> skipDownload, CancellationToken cancellationToken = default)
    {
        if (imageLink.IsInvalid)
        {
            if (SiteName == "e-hentai")
            {
                // Used to force generation of unparsed links
                // Using a partial parsing approach, so this may be called multiple times per rip
                throw new EHentaiUrlExpiredException();
            }

            // This should never happen for any other site
            throw new RipperException("Invalid ImageLink found for non-EHentai site");
        }

        var url = imageLink.Url;
        await Task.Delay((int)(SleepTime * MillisecondsInSecond), cancellationToken);
        var (modifiedHeader, oldCookies) = await ModifyHeaders(url, imageLink, cancellationToken);

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
                return await HandleUnsuccessfulStatusCode(response, url, imageLink, generatingManually, skipDownload,
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

            if (imageLink.LinkInfo == LinkInfo.GoFile)
            {
                var ext = FileUtility.GetCorrectExtension(imagePath);
                if (ext != ".html")
                {
                    return true;
                }

                Logger.Warning("GoFile download failed, trying again...");
                await AssociateGoFileCookies(imageLink.Url, cancellationToken);
                return false;
            }

            return true;
        }
    }

    private async Task<(ModifiedHeader modifiedHeader, string oldCookies)> ModifyHeaders(
        string url, ImageLink imageLink, CancellationToken cancellationToken = default)
    {
        var modifiedHeader = ModifiedHeader.None;
        var oldCookies = "";
        if (url.Contains("redgifs"))
        {
            modifiedHeader = ModifiedHeader.Authorization;
            var token = await TokenManager.GetToken(TokenKey.Redgifs, cancellationToken);
            RequestHeaders[RequestHeaderKeys.Authorization] = $"Bearer {token.Value}";
        }
        else if (imageLink.LinkInfo == LinkInfo.GoFile)
        {
            modifiedHeader = ModifiedHeader.Cookie;
            var cookieValue = Config.Custom.GoFile.AccountToken;
            var cookie = $"accountToken={cookieValue}";
            oldCookies = RequestHeaders[RequestHeaderKeys.Cookie];
            RequestHeaders[RequestHeaderKeys.Cookie] = cookie;
        }
        else if (imageLink.Url.Contains("donmai.us"))
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

    private async Task<bool> HandleUnsuccessfulStatusCode(HttpResponseMessage response, string url, ImageLink imageLink,
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
                var parts = imageLink.Url.Split(".");
                var ext = parts[^1];
                if (PixivExtMap.TryGetValue(ext, out var mappedExt))
                {
                    parts[^1] = mappedExt;
                    imageLink.Url = string.Join(".", parts);
                    Logger.Information("Trying again with .{MappedExt} extension...", mappedExt);
                }
                else
                {
                    Logger.Warning("Unable to download Pixiv image: {URL}", imageLink.Url);
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
                        throw new EHentaiUrlExpiredException();
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
            return SiteName == "e-hentai" ? throw new EHentaiUrlExpiredException() : DownloadStatus.Failed;
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

    private byte[] GetImageViaSelenium(string url)
    {
        Driver.Url = url;
        var b64Img = (string?)Driver.ExecuteScript("""
                                                   const img = document.getElementsByTagName("img")[0];
                                                   const canvas = document.createElement("canvas");
                                                   canvas.width = img.naturalWidth;
                                                   canvas.height = img.naturalHeight;
                                                   const ctx = canvas.getContext("2d");
                                                   ctx.drawImage(img, 0, 0);
                                                   const dataURL = canvas.toDataURL("image/png");
                                                   return dataURL.replace(/^data:image\/(png|jpg);base64,/, "");
                                                   """);
        var bytes = Convert.FromBase64String(b64Img!);
        return bytes;
    }

    private static void LogFailedUrl(string url)
    {
        using var writer = new StreamWriter("failed.txt", true, Encoding.Unicode);
        writer.WriteLine(url);
    }

    /*private static void PrintDebugInfo(string title, string fd = "output.txt", bool clear = false, params object[] data)
    {
        using var writer = new StreamWriter(fd, !clear, Encoding.Unicode);
        writer.WriteLine($"[{title}]");
        foreach (var d in data)
        {
            writer.WriteLine($"\t{d.ToString()?.Trim()}");
        }
    }*/

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