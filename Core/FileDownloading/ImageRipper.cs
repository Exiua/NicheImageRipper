using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Common.ExtensionMethods;
using Core.Configuration;
using Core.DataStructures;
using Core.Driver;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.SiteParsing;
using Core.SiteParsing.HtmlParsers;
using Core.Utility;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using ImageMagick;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using Serilog;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using WebDriver = Core.Driver.WebDriver;

namespace Core.FileDownloading;

public partial class ImageRipper : IDisposable
{
    internal const string RipIndexPath = ".ripIndex";
    
    private const string YoutubeCookiesFile = "yt_cookies.txt";
    private const string RipStatePath = "ripState.json";
    private const int RetryCount = 4;
    private const int MillisecondsInSecond = 1000;
    private const int MinimumFileSize = 1024; // 1KB minimum file size

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
    private ApiClientManager ClientManager { get; }
    public bool Paused { get; set; }
    
    private bool _disposed;
    
    private FirefoxDriver Driver => WebDriver.Driver;

    private static GeneralConfig Config => Configuration.Config.Instance;
    private static TokenManager TokenManager => TokenManager.Instance;
    private static FlareSolverrManager FlareSolverrManager => NicheImageRipper.FlareSolverrManager;
    
    public delegate void ProgressChangedHandler(int current, int total);
    public event ProgressChangedHandler? OnProgressChanged;

    public ImageRipper(WebDriverPool driverPool, FilenameScheme filenameScheme = FilenameScheme.Original,
                       UnzipProtocol unzipProtocol = UnzipProtocol.None, PostDownloadAction postDownloadAction = PostDownloadAction.None)
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
        ClientManager = new ApiClientManager();
    }

    public async Task Rip(string url)
    {
        // Cannot rip if the instance is disposed, but everything else is fine to access
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ImageRipper), "Cannot use a disposed ImageRipper instance");
        }
        
        OnProgressChanged?.Invoke(0, 0); // Indeterminate progress at the start
        SleepTime = 0.2f;   // Reset sleep time
        GivenUrl = url.Replace("members.", "www."); // Replace is done to properly parse hanime pages
        (SiteName, SleepTime) = UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        Log.Debug("Site Name: {SiteName}", SiteName);
        Log.Debug("Checking if cookies are needed for {SiteName}", SiteName);
        if (CookiesNeeded())
        {
            Log.Debug("Adding cookies needed for {SiteName}", SiteName);
            AddCookies();
        }

        await FileGetter();
    }

    private void LoadCorrectWebDriver()
    {
        // TODO: Figure out better implementation for this
        if (SiteName == "quatvn")
        {
            if (WebDriver.IsHeadless)
            {
                Log.Debug("Releasing headless driver");
                DriverPool.ReleaseDriver(WebDriver);
                // Need non-headless driver for quatvn
                Log.Debug("Acquiring non-headless driver");
                WebDriver = DriverPool.AcquireDriver(false);
            }
        }
        else
        {
            if (!WebDriver.IsHeadless)
            {
                Log.Debug("Releasing non-headless driver");
                DriverPool.ReleaseDriver(WebDriver);
                // Can use headless driver for other sites
                Log.Debug("Acquiring headless driver");
                WebDriver = DriverPool.AcquireDriver(true);
            }
        }
    }

    private async Task<int> GetStartIndex()
    {
        int start;
        if (File.Exists(RipIndexPath))
        {
            var index = await File.ReadAllTextAsync(RipIndexPath);
            start = int.Parse(index);
            Log.Information("Resuming from index {StartIndex}", start);
            File.Delete(RipIndexPath);
        }
        else
        {
            start = FolderInfo.MustGenerateManually ? 1 : 0;
        }
        
        return start;
    }

    private async Task FileGetter()
    {
        LoadCorrectWebDriver();
        
        var htmlParser = HtmlParser.GetParser(SiteName, WebDriver, ClientManager, RequestHeaders, FilenameScheme);
        Log.Debug("Constructed HtmlParser");
        FolderInfo = await htmlParser.ParseSite(GivenUrl);
        //Log.Debug("Folder Info: {@FolderInfo}", FolderInfo);
        //Log.Debug("Directory Name: {DirectoryName}", FolderInfo.DirectoryName);
        var fullPath = Path.Combine(SavePath, FolderInfo.DirectoryName);
        if (Interrupted && FilenameScheme != FilenameScheme.Hash)
        {
            // TODO: self.folder_info.urls = self.get_incomplete_files(full_path)
        }
        
        Log.Debug("Dir Length: {DirLength}", fullPath.Length);
        Directory.CreateDirectory(fullPath);

        var start = await GetStartIndex();
        DownloadStats downloadStats;
        HashSet<HashKey> filesHashes;
        if (!File.Exists(RipStatePath))
        {
            downloadStats = new DownloadStats();
            filesHashes = [];
        }
        else
        {
            var state = JsonUtility.Deserialize<RipState>(RipStatePath);
            if (state is null)
            {
                Log.Error("Failed to load rip state");
                throw new RipperException("Failed to load rip state");
            }
            
            downloadStats = state.DownloadStats;
            filesHashes = DeserializeHashKeyHashSet(state.FilesHashes);
        }
        
        try
        {
            // Can get the image through numerically ascending url for imhentai and hentairox
            //   (hard to account for gifs and other extensions otherwise)
            if (FolderInfo.MustGenerateManually)
            {
                await HandleGeneratingManually(start, fullPath, filesHashes, downloadStats);
            }
            // Easier to put all image url in a list and then download for these sites
            else
            {
                await HandleDownloadingFromList(start, fullPath, filesHashes, downloadStats);
            }
        }
        catch
        {
            Log.Debug("Saving rip state due to exception");
            var state = new RipState
            {
                DownloadStats = downloadStats,
                FilesHashes = SerializeHashKeyHashSet(filesHashes)
            };
            
            JsonUtility.Serialize(RipStatePath, state);
            Log.Debug("Saved rip state to {RipStatePath}", RipStatePath);
            throw;
        }
        
        File.Delete(RipIndexPath);
        File.Delete(RipStatePath); // Existence of rip state file indicates incomplete rip
        if(((double)downloadStats.FailedDownloadsCount) / FolderInfo.NumUrls > FailureThreshold)
        {
            var msg = $"More than {FailureThreshold*100}% of the images failed to download";
            var e = new RipperException(msg);
            Log.Error(e, "{msg:l}", msg);
            if (SiteName == "sexbjcam")
            {
                File.Delete("partial.json");
            }
            
            throw e;
        }
        
        if(UnzipProtocol != UnzipProtocol.None)
        {
            UnzipFiles(fullPath, downloadStats);
        }

        if (downloadStats.HasFailedDownloads)
        {
            var failedDownloads = downloadStats.FailedDownloads.Join("\n");
            var failedPath = Path.Combine(fullPath, "failedDownloads.txt");
            await File.WriteAllTextAsync(failedPath, failedDownloads);
        }

        var downloadResults = downloadStats.GetStats(FolderInfo.NumUrls);
        Log.Information("{Results:l}", downloadResults);
        Log.Information("Download Complete");
        OnProgressChanged?.Invoke(1, 1); // Complete progress at the end
    }

    private static List<string> SerializeHashKeyHashSet(HashSet<HashKey> hashKeys)
    {
        return hashKeys.Select(hashKey => Convert.ToHexString(hashKey.Hash)).ToList();
    }

    private static HashSet<HashKey> DeserializeHashKeyHashSet(List<string> hashKeys)
    {
        return hashKeys.Select(hashKey => new HashKey(Convert.FromHexString(hashKey))).ToHashSet();
    }
    
    private static Task Sleep(int milliseconds)
    {
        return Task.Delay(milliseconds);
    }

    private async Task HandleGeneratingManually(int start, string fullPath, HashSet<HashKey> filesHashes, DownloadStats downloadStats)
    {
        // Gets the general url for all images in this album
        var imageLink = FolderInfo.Urls[0];
        var trimmedUrl = TrimUrl(imageLink.Url);
        imageLink.Url = trimmedUrl;
        string[] extensions = [".webp", ".jpg", ".gif", ".png" , ".webm", ".mp4", "t.jpg"];
            
        // Downloads all images from the general url by incrementing the file number
        //  (e.g., https://domain/gallery/##.jpg)
        for (var index = start; index < FolderInfo.NumUrls + 1; index++)
        {
            CurrentIndex = index;
            OnProgressChanged?.Invoke(index, FolderInfo.NumUrls + 1);

            while (Paused)
            {
                await Sleep(1000);
            }

            foreach (var (i, ext) in extensions.Enumerate())
            {
                try
                {
                    var fullFilename = $"{index}{ext}";
                    var imagePath = Path.Combine(fullPath, fullFilename);
                    var success = await DownloadFromUrl(imageLink, index.ToString(), imagePath, ext);
                    if (success)
                    {
                        await PostProcess(imageLink, imagePath, filesHashes, downloadStats);
                    }
                    
                    break;
                }
                catch // TODO: Narrow down exceptions
                {
                    if (i == 3)
                    {
                        downloadStats.FailedDownloads.Add($"{imageLink.Url}{index}.{ext}]");
                        Log.Warning("Image not found");
                    }
                }
            }
        }
    }

    private async Task HandleDownloadingFromList(int start, string fullPath, HashSet<HashKey> filesHashes, DownloadStats downloadStats)
    {
        switch (SiteName)
        {
            case "deviantart":
                // Delegated to external tool
                await DeviantArtDownload(fullPath, FolderInfo.Urls[0].Url);
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
                                await semaphore.WaitAsync();
                                if (completed[i])
                                {
                                    return;
                                }
                                
                                for (var attempt = 0; attempt < maxAttempts; attempt++)
                                {
                                    while (Paused)
                                    {
                                        await Sleep(1000);
                                    }
                                    
                                    try
                                    {
                                        await DownloadSingleFromList(i, link, fullPath, filesHashes, downloadStats);
                                        Interlocked.Increment(ref complete);
                                        completed[i] = true;
                                        Log.Information("Finished downloading {Index}, {Total} remaining", i + 1, total - complete);
                                        OnProgressChanged?.Invoke(complete + 1, total + 1);
                                        break;
                                    }
                                    catch (Exception e)
                                    {
                                        if (attempt == maxAttempts - 1)
                                        {
                                            Log.Error(e, "Error downloading {Index}, {Total} remaining: {Url}", i + 1, total - complete, link.Url);
                                        }
                                        else
                                        {
                                            Log.Warning("Error downloading {Index}, {Total} remaining: {Url}, retrying... ({Attempt}/{MaxAttempts})", i + 1, total - complete, link.Url, attempt + 1, maxAttempts);
                                            await Sleep(1000);
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
                                    await Sleep(1000);
                                }
                                
                                // Compute the absolute index (i is the relative index after start)
                                var index = start + i;
                                await DownloadSingleFromList(index, link, fullPath, filesHashes, downloadStats, true);
                            }
                        }

                        break;
                    }
                    catch (EHentaiUrlExpiredException e)
                    {
                        Log.Information("Refreshing EHentai links");
                        var parser = new EHentaiParser(WebDriver, ClientManager, RequestHeaders, FilenameScheme);
                        start = e.ResumeIndex;
                        var updatedLinks = await parser.UpdateLinks(FolderInfo.Urls, start);
                        FolderInfo.Urls = updatedLinks;
                    }
                }

                break;
            }
        }
    }

    private async Task DownloadSingleFromList(int index, ImageLink link, string fullPath, HashSet<HashKey> filesHashes,
                                              DownloadStats downloadStats, bool updateProgress = false)
    {
        Log.Debug("Index: {Index}, Total: {Total}", index, FolderInfo.NumUrls);
        if (updateProgress)
        {
            OnProgressChanged?.Invoke(index + 1, FolderInfo.NumUrls + 1);
        }
        
        CurrentIndex = index;
        while (Paused)
        {
            await Sleep(1000);
        }

        await Task.Delay((int)SleepTime * MillisecondsInSecond);
        try
        {
            var filename = link.Filename;
            var imagePath = Path.Combine(fullPath, filename);
            var success = await DownloadFromList(link, imagePath, index, downloadStats);
            if (success)
            {
                // DownloadFromList may modify filename (if it was missing extension)
                imagePath = Path.Combine(fullPath, link.Filename); 
                await PostProcess(link, imagePath, filesHashes, downloadStats);
            }
        }
        catch (FileNotFoundException)
        {
            Log.Warning("File not found: {Url}", link.Url);
            if (link.LinkInfo == LinkInfo.IframeMedia)
            {
                downloadStats.FailedDownloads.Add(link.Url);
                await File.AppendAllTextAsync("failed_iframe.txt", $"{link.Url} {link.Referer}\n");
            }
        }
        catch (EHentaiUrlExpiredException e)
        {
            Log.Debug("Caught EHentaiUrlExpiredException, need to refresh links");
            e.ResumeIndex = index;
            throw;
        }
        catch (Exception e)
        {
            Log.Debug("Caught exception, saving progress. Reason: {ErrorMessage}", e.Message);
            await File.WriteAllTextAsync(".ripIndex", CurrentIndex.ToString());
            throw;
        } 
    }

    private async Task PostProcess(ImageLink link, string imagePath, HashSet<HashKey> filesHashes,
                                   DownloadStats downloadStats)
    {
        if (PostDownloadAction.HasFlag(PostDownloadAction.RemoveDuplicates))
        {
            // Maybe handle directory downloads (e.g., Mega) in the future?
            // Unable to determine filename before downloading files/directories from Mega.nz
            // Could probably guess file ext for youtube videos though (as we have filestem already)
            if (Directory.Exists(imagePath) || link.LinkInfo == LinkInfo.Mega || link.LinkInfo == LinkInfo.YoutubeVideo)
            {
                return;
            }
            
            var duplicate = await HandleDuplicateFile(imagePath, filesHashes);
            if (duplicate)
            {
                downloadStats.NumDuplicates++;
            }
        }
    }
    
    private static async Task<bool> DeviantArtDownload(string fullPath, string url)
    {
        var cmd = new []{"-D", $"\"{fullPath}\"", "-u", Config.Logins.DeviantArt.Username, "-p", 
            Config.Logins.DeviantArt.Password, "--write-log", "log.txt", url};
        var (exitCode, _, _) = await RunSubprocess("gallery-dl", cmd, startMessage: "Starting Deviantart download",
            endMessage: "Deviantart download finished");
        if (exitCode != 0)
        {
            Log.Error("Failed to download from DeviantArt");
        }
        
        return exitCode == 0;
    }
    
    internal static async Task<FfmpegStatusCode> RunFfmpeg(string[] cmd, string startMessage = "Starting ffmpeg download", string endMessage = "Ffmpeg download finished",
        bool displayOutput = false)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.Ffmpeg))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.Ffmpeg);
        }
        
        if (!displayOutput)
        {
            cmd = [ "-loglevel", "quiet", "-y", ..cmd ];
        }
        else
        {
            cmd = [ "-y", ..cmd ];
        }
        Log.Debug("ffmpeg {cmd}", string.Join(" ", cmd));
        var (exitCode, _, _) = await RunSubprocess("ffmpeg", cmd, captureError: displayOutput,
            startMessage: startMessage, endMessage: endMessage);
        if (exitCode != 0)
        {
            Log.Error("Failed to run ffmpeg: {ExitCode}", exitCode);
        }
        
        return (FfmpegStatusCode)exitCode;
    }

    private static async Task<bool> RunYtDlp(ImageLink link, string path, string startMessage, string endMessage)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.YtDlp))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.YtDlp);
        }

        var url = link.Url;
        var parent = Directory.GetParent(path)!.FullName;
        var filename = Path.GetFileName(path);
        string[] cmd = link.Referer != "" ?
            [
                "--force-overwrites",
                "-P", $"\"{parent}\"", 
                "-o", $"\"{filename}\"",
                "--add-headers", $"\"Referer:{link.Referer}\"",
                $"\"{url}\"",
            ] :
            [
                "--force-overwrites",
                "-P", $"\"{parent}\"", 
                "-o", $"\"{filename}\"",
                $"\"{url}\"",
            ];
        //cmd = [ "--no-warnings", ..cmd ];
        Log.Debug("yt-dlp {cmd}", string.Join(" ", cmd));
        var (exitCode, output, _) = await RunSubprocess("yt-dlp", cmd, true,/* true,*/ 
            startMessage: startMessage, endMessage: endMessage);

        if (exitCode == 0)
        {
            return true;
        }

        var lines = output!.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ageRestricted = lines.Any(line => line.Contains("This video is age-restricted"));
        if (ageRestricted)
        {
            if (!File.Exists(YoutubeCookiesFile))
            {
                Log.Error("Video is age-restricted but no cookies file found at {YoutubeCookiesFile}", YoutubeCookiesFile);
                return false;
            }
            
            Log.Information("Video is age-restricted, trying again with cookies");
            cmd = [
                "--force-overwrites",
                "--cookies", $"\"{YoutubeCookiesFile}\"",
                "-P", $"\"{parent}\"", 
                "-o", $"\"{filename}\"",
                $"\"{url}\"",
            ];
            
            (exitCode, _, _) = await RunSubprocess("yt-dlp", cmd, true,/* true,*/ 
                startMessage: startMessage, endMessage: endMessage);
        }
        
        Log.Error("Failed to run yt-dlp: {ExitCode}", exitCode);

        return exitCode == 0;
    }
    
    private static async Task<(int, string?, string?)> RunSubprocess(string executable, string[]? arguments = null,
                                                                     bool captureOutput = false, bool captureError = false,
                                                                     string? startMessage = null, string? endMessage = null)
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
        
        await process.WaitForExitAsync();
        var exitCode = process.ExitCode;
        if (endMessage is not null)
        {
            Log.Information("{EndMessage:l}", endMessage);
        }
        
        var outputStr = output?.ToString();
        var errorStr = error?.ToString();
        
        return (exitCode, outputStr, errorStr);
    }

    private static async Task<bool> HandleDuplicateFile(string imagePath, HashSet<HashKey> filesHashes)
    {
        var fileHash = await FileUtility.GetFileHash(imagePath);
        if (!filesHashes.Add(fileHash))
        {
            Log.Information("Duplicate file detected: {ImagePath}", imagePath);
            File.Delete(imagePath);
            return true;
        }

        Log.Debug("File hash: {FileHash}", fileHash);
        return false;
    }
    
    /// <summary>
    ///     Download image from image url
    /// </summary>
    /// <param name="imageLink">ImageLink containing data on the file to download</param>
    /// <param name="filename">Name of the file to download</param>
    /// <param name="imagePath">Full path to download the file to</param>
    /// <param name="ext">Extension of the file to download</param>
    private async Task<bool> DownloadFromUrl(ImageLink imageLink, string filename, string imagePath, string ext)
    {
        var numFiles = FolderInfo.NumUrls;
        // Completes the specific image URL from the general URL
        var url = imageLink.Url;
        var fullFilename = $"{filename}{ext}";
        var ripUrl = $"{url}{fullFilename}";
        var numProgress = $"({filename}/{numFiles})";
        Log.Information("{RipUrl:l}    {NumProgress:l}", ripUrl, numProgress);
        imageLink.Url = ripUrl;
        bool success;
        try
        {
            success = await DownloadFile(imagePath, imageLink, true);
        }
        finally
        {
            imageLink.Url = url;
        }
        
        await Sleep(50);
        return success;
    }

    /// <summary>
    ///     Download images from url supplied from a list of image urls
    /// </summary>
    /// <param name="imageLink">ImageLink containing data on the file to download</param>
    /// <param name="imagePath">Full path of the location to save the file to</param>
    /// <param name="currentFileNum">Number of the file being downloaded</param>
    /// <param name="downloadStats">DownloadStats object to update with results</param>
    private async Task<bool> DownloadFromList(ImageLink imageLink, string imagePath, int currentFileNum,
                                              DownloadStats downloadStats)
    {
        var numFiles = FolderInfo.NumUrls;
        var ripUrl = imageLink.Url;
        var displayUrl = imageLink.LinkInfo == LinkInfo.Base64 ? UrlUtility.TruncateLongUrl(ripUrl) : ripUrl;
        Log.Information("{Url:l}    ({CurrentProgress}/{TotalProgress})", displayUrl, currentFileNum + 1, numFiles);
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
                success = await DownloadM3U8ToMp4(imagePath, imageLink);
                if (!success)
                {
                    success = await DownloadObfuscatedM3U8(imagePath, imageLink);
                }
                break;
            case LinkInfo.M3U8YtDlp:
                success = await DownloadM3U8YtDlp(imagePath, imageLink);
                if (!success)
                {
                    success = await DownloadObfuscatedM3U8(imagePath, imageLink);
                }
                break;
            case LinkInfo.ObfuscatedM3U8:
                success = await DownloadObfuscatedM3U8(imagePath, imageLink);
                break;
            case LinkInfo.GDrive:
                success = await DownloadGDriveFile(imagePath, imageLink);
                break;
            case LinkInfo.IframeMedia:
                success = await DownloadIframeMedia(imagePath, imageLink);
                // TODO: Figure out how to delete temp directories
                break;
            case LinkInfo.Mega:
                success = await DownloadMegaFiles(imagePath, imageLink);
                Log.Debug("Success from Mega: {Success}", success);
                break;
            case LinkInfo.PixelDrain:
                success = await DownloadPixelDrainFiles(imagePath, imageLink);
                break;
            case LinkInfo.YoutubeVideo:
                success = await DownloadYoutubeVideo(imagePath, imageLink);
                await Sleep(1250);
                break;
            case LinkInfo.Text:
                await File.AppendAllTextAsync(imagePath, ripUrl + "\n");
                success = true;
                break;
            case LinkInfo.MpegDash:
                success = await DownloadMpegDashFile(imagePath, imageLink);
                break;
            case LinkInfo.ResolveImage:
                success = await ResolveAndDownloadFile(imagePath, imageLink);
                break;
            case LinkInfo.SeleniumImage:
                success = await DownloadSeleniumImage(imagePath, imageLink);
                break;
            case LinkInfo.Base64:
                success = await DownloadBase64Image(imagePath, imageLink);
                break;
            case LinkInfo.PixivUgoira:
                success = await DownloadPixivUgoira(imagePath, imageLink);
                 break;
            case LinkInfo.GoFile:
            case LinkInfo.None:
                success = await DownloadFile(imagePath, imageLink, false);
                break;
            default:
                var e = new RipperException("Unknown LinkInfo: " + imageLink.LinkInfo);
                Log.Error(e, "Unknown LinkInfo: {LinkInfo}", imageLink.LinkInfo);
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
        await Sleep(50);

        return success;
    }

    private async Task<bool> ResolveAndDownloadFile(string path, ImageLink imageLink)
    {
        var url = imageLink.Url;
        for (var i = 0; i < RetryCount; i++)
        {
            var imageUrl = await GetDownloadUrl(url);
            if (imageUrl == "")
            {
            
                await Sleep(500);
                continue;
            }
            
            Log.Debug("Resolved URL: {Url}", imageUrl);
            imageLink.Url = imageUrl;

            var success = await DownloadFile(path, imageLink, false);
            if (success)
            {
                return true;
            }

            await Sleep(500);
        }
        
        return false;
    }

    private async Task<string> GetDownloadUrl(string url)
    {
        var siteName = url.Split('.')[1];
        using var request = RequestHeaders.ToRequest(HttpMethod.Get, url);
        var response = await Session.SendAsync(request, HttpCompletionOption.ResponseContentRead);
        if (!response.IsSuccessStatusCode)
        {
            Log.Error("<Response {ErrorCode}> Failed to get download url: {Url}",  response.StatusCode, url);
            return "";
        }

        if (response.RequestMessage!.RequestUri!.ToString() == $"https://www.{siteName}.com/hcaptcha.aspx")
        {
            Log.Information("Captcha detected, solving...");
            await SolveCaptcha(url, true);
            var reRequest = RequestHeaders.ToRequest(HttpMethod.Get, url);
            response = await Session.SendAsync(reRequest, HttpCompletionOption.ResponseContentRead);
            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Failed to get download url: {Url}", url);
                return "";
            }
        }
        
        Log.Information("Getting download url from {Url}", url);
        var content = await response.Content.ReadAsStringAsync();
        var match = NLegsImageUrlRegex().Match(content);
        return $"https://www.{siteName}.com" + match.Groups[1].Value;
    }
    
    private static async Task SolveCaptcha(string url, bool humanSolve)
    {
        await FlareSolverrManager.GetSiteSolution(url);
        if (humanSolve)
        {
            Log.Information("Solve the captcha and press enter to continue");
            Console.ReadLine();
        }
    }

    private static async Task<bool> DownloadMpegDashFile(string filePath, ImageLink imageLink)
    {
        var parent = Directory.GetParent(filePath)!.FullName;
        var filename = Path.GetFileName(filePath);
        var cmd = new[] { "-P", $"\"{parent}\"", imageLink.Url, "-o", filename };
        var (exitCode, _, _) = await RunSubprocess("yt-dlp", cmd, startMessage: "Starting youtube-dl download",
            endMessage: "youtube-dl download finished");
        return exitCode == 0;
    }

    private static async Task<bool> DownloadM3U8ToMp4(string filePath, ImageLink imageLink)
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
        
        var result = await RunFfmpeg(cmd);
        Log.Debug("Ffmpeg result: {Result}", result.GetShortErrorMessage());
        return result.IsSuccess();
    }

    private static Task<bool> DownloadM3U8YtDlp(string filePath, ImageLink imageLink)
    {
        return RunYtDlp(imageLink, filePath, startMessage: "Starting yt-dlp download",
            endMessage: "yt-dlp download finished");
    }
    
    private static async Task<bool> DownloadGDriveFile(string filePath, ImageLink imageLink)
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
        await request.DownloadAsync(stream);
        return true;
    }
    
    private static async Task<bool> DownloadIframeMedia(string filePath, ImageLink imageLink)
    {
        var parentPathInfo = Directory.GetParent(filePath)!;
        var parentPath = parentPathInfo.FullName; 
        Directory.CreateDirectory(parentPath);
        for(var i = 0; i < RetryCount; i++)
        {
            try
            {
                var video = new BunnyVideoDrm(
                    referer: imageLink.Url,
                    embedUrl: imageLink.Referer!,
                    name: Path.GetFileName(filePath).Split('.')[0],
                    path: parentPath
                );
                await video.Download();
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
    
    private async Task<bool> DownloadMegaFiles(string filePath, ImageLink imageLink)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.MegaCmd))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.MegaCmd);
        }
        
        Log.Debug("Logging in to MegaCmd");
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
        
        if(!PersistentLogins["Mega"])
        {
            var e = new RipperException("Unable to login to MegaCmd");
            Log.Error(e, "Unable to login to MegaCmd");
            throw e;
        }

        if (imageLink.Url.Contains("/file/"))
        {
            Log.Debug("Downloading file from Mega: {Url}", imageLink.Url);
            filePath = Path.GetDirectoryName(filePath)!;
        }
        else
        {
            Log.Debug("Downloading folder from Mega: {Url}", imageLink.Url);
            Directory.CreateDirectory(filePath);
        }

        while (true)
        {
            // TODO: Need better way to check if megacmd has timeout or is just downloading large amounts of data
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(60));
            try
            {
                return await MegaApi.DownloadAsync(imageLink.Url, filePath, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Mega download timed out, retrying...");
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to download from Mega: {Url}", imageLink.Url);
                if (e.Message.Contains("No such file or directory"))
                {
                    Log.Error("The specified file or directory does not exist on Mega: {Url}", imageLink.Url);
                    return false;
                }
                
                if (e.Message.Contains("Invalid URL"))
                {
                    Log.Error("The provided URL is invalid: {Url}", imageLink.Url);
                    return false;
                }
                
                throw; // Re-throw the exception for further handling
            }
        }
    }
    
    private static async Task<bool> DownloadPixelDrainFiles(string filePath, ImageLink imageLink)
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
        
        var response = await client.GetAsync($"https://pixeldrain.com/api/file/{imageLink.Url}", HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }
        
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fileStream);
        return true;
    }

    private static Task<bool> DownloadYoutubeVideo(string filePath, ImageLink imageLink)
    {
        return RunYtDlp(imageLink, filePath, startMessage: "Starting youtube-dl download",
            endMessage: "youtube-dl download finished");
    }

    private async Task<bool> DownloadSeleniumImage(string filePath, ImageLink imageLink)
    {
        try
        {
            var imageData = GetImageViaSelenium(imageLink.Url);
            await File.WriteAllBytesAsync(filePath, imageData);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to download image");
            return false;
        }
    }
    
    private static async Task<bool> DownloadBase64Image(string filePath, ImageLink imageLink)
    {
        try
        {
            var base64Data = imageLink.Url.Split(',')[1];
            var imageData = Convert.FromBase64String(base64Data);
            await File.WriteAllBytesAsync(filePath, imageData);
            return true;
        }
        catch (FormatException e)
        {
            Log.Error(e, "Failed to decode base64 image");
            return false;
        }
    }

    private static async Task<bool> DownloadObfuscatedM3U8(string filePath, ImageLink imageLink)
    {
        try
        {
            var parent = Directory.GetParent(filePath)!.FullName;
            var referer = imageLink.Referer == "" ? null : imageLink.Referer;
            await M3U8Downloader.DownloadObfuscatedM3U8(imageLink.Url, parent, imageLink.Filename, referer);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to download obfuscated M3U8");
            return false;
        }
    }
    
    private async Task<bool> DownloadPixivUgoira(string filePath, ImageLink imageLink)
    {
        var illustId = imageLink.Url.Split("/")[4];
        var metadataUrl = $"https://www.pixiv.net/ajax/illust/{illustId}/ugoira_meta";
        Log.Debug("Fetching Pixiv Ugoira metadata from {MetadataUrl}", metadataUrl);
        // Should contain PHPSESSID (checked in PixivParser)
        var sessionId = TokenManager.GetTokenWithRotation(RotationKey.Pixiv, TimeSpan.FromHours(24), Config.Cookies.Pixiv); 
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
        HttpResponseMessage response = null!;       // Must be assigned before exiting loop
        List<(string, int)> framesMetadata = null!;
        foreach (var (i, key) in keys.Enumerate())
        {
            var src = body[key]?.GetValue<string>();
            if (src is null)
            {
                Log.Warning("Pixiv Ugoira source not found for key: {Key}", key);
                if (i == keys.Length - 1)
                {
                    RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
                    throw new RipperException("Pixiv Ugoira source not found");
                }
                
                continue;
            }

            var request = RequestHeaders.ToRequest(HttpMethod.Head, src);
            response = await Session.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Failed to access Pixiv Ugoira source: {Src}", src);
                if (i == keys.Length - 1)
                {
                    RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
                    throw new RipperException("Unable to access Pixiv Ugoira source");
                }

                continue;
            }
            
            request = RequestHeaders.ToRequest(HttpMethod.Get, src);
            response = await Session.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Failed to download Pixiv Ugoira: {Src}", src);
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

        await using var zipStream = await response.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        using var animation = new MagickImageCollection();
        foreach (var (fileName, delay) in framesMetadata)
        {
            var entry = archive.GetEntry(fileName);
            if (entry is null)
            {
                Log.Warning("Warning: {FileName} not found in ZIP", fileName);
                continue;
            }

            await using var entryStream = entry.Open();
            var img = new MagickImage(entryStream)
            {
                AnimationDelay = (uint)(delay / 10) // Convert milliseconds to centiseconds
            };
            animation.Add(img);
        }

        animation[0].AnimationIterations = 0;
        //animation.OptimizeTransparency();
        await animation.WriteAsync(filePath);
        RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
        TokenManager.UpdateTokenRotation(RotationKey.Pixiv);
        return true;
    }

    private async Task<bool> DownloadFile(string filePath, ImageLink imageLink, bool generatingManually)
    {
        if(filePath[^1] == '/')
        {
            filePath = filePath[..^1];
        }

        var success = false;
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            success = await DownloadFileHelper(imageLink, filePath, generatingManually);
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
        if (Path.GetExtension(filePath) == "")
        {
            Log.Debug("Finding correct extension for file: {ImagePath}", filePath);
            var extension = FileUtility.GetCorrectExtension(filePath);
            await RenameFile(filePath, filePath + extension);
            var filename = Path.GetFileName(filePath);
            var newFilename = filename + extension;
            Log.Debug("Renamed file {OldFilename} to {NewFilename}", filename, newFilename);
            imageLink.Filename = newFilename;
        }
        
        return true;
    }

    private async Task<bool> DownloadFileHelper(ImageLink imageLink, string imagePath, bool generatingManually)
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
        await Task.Delay((int)(SleepTime * MillisecondsInSecond));
        var (modifiedHeader, oldCookies) = await ModifyHeaders(url, imageLink);

        Log.Debug("Request Headers: {@RequestHeaders}", RequestHeaders);
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
                    Log.Information("Resuming download from byte {Offset}", resumeFrom);
                }
                
                response = await Session.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (HttpRequestException e) when (e.InnerException is InvalidOperationException)
            {
                Log.Error("Unable to establish a connection to {Url}", url);
                return false;
            }
            catch (HttpRequestException e) when (e.InnerException is SocketException)
            {
                Log.Error("Unable to establish a connection to {Url}", url);
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                return await HandleUnsuccessfulStatusCode(response, url, imageLink, generatingManually);
            }

            DownloadStatus result;
            try
            {
                result = await WriteToFile(response, imagePath, resumeFrom);
            }
            catch (DownloadTimeoutException e)
            {
                if (e.DownloadedBytesCount == resumeFrom)
                {
                    Log.Warning("No progress made during download, aborting...");
                    throw;
                }
                
                resumeFrom += e.DownloadedBytesCount;
                continue;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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

                Log.Warning("GoFile download failed, trying again...");
                await AssociateGoFileCookies(imageLink.Url);
                return false;
            }

            return true;
        }
    }

    private async Task<(ModifiedHeader modifiedHeader, string oldCookies)> ModifyHeaders(string url, ImageLink imageLink)
    {
        var modifiedHeader = ModifiedHeader.None;
        var oldCookies = "";
        if (url.Contains("redgifs"))
        {
            modifiedHeader = ModifiedHeader.Authorization;
            var token = await TokenManager.GetToken(TokenKey.Redgifs);
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

    private async Task<bool> HandleUnsuccessfulStatusCode(HttpResponseMessage response, string url, ImageLink imageLink, bool generatingManually)
    {
        Log.Warning("<Response {ResponseStatusCode}>", response.StatusCode);
        await Sleep(500);
        
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
                // Api seems to always return .jpg even if the file is a .png
                var parts = imageLink.Url.Split(".");
                var ext = parts[^1];
                if (ext == "jpg")
                {
                    parts[^1] = "png";
                    imageLink.Url = string.Join(".", parts);
                    Log.Information("Trying again with .png extension...");
                }

                return false;
            }
            case HttpStatusCode.Unauthorized:
                return false;
            case HttpStatusCode.Forbidden:
                switch (SiteName)
                {
                    case "kemono" when !url.Contains(".psd") && url.Contains("kemono"):
                        Log.Information("Wrong subdomain, trying again...");
                        throw new BadSubdomainException();
                    case "e-hentai":
                        Log.Information("E-Hentai URL expired, trying to update links...");
                        await Task.Delay(10 * MillisecondsInSecond); // Wait for 10 seconds before retrying
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
                Log.Warning("Unhandled status code: {ResponseStatusCode}", response.StatusCode);
                return false;
            
            #endregion
        }
    }
    
    private async Task AssociateGoFileCookies(string url)
    {
        Log.Debug("Associating GoFile cookies");
        var siteLoginStatus = WebDriver.SiteLoginStatus;
        try
        {
            if(!siteLoginStatus.GetValueOrDefault("gofile", false))
            {
                Log.Debug("Logging into GoFile");
                siteLoginStatus["gofile"] = await GoFileLogin();
            }
            
            Driver.Url = url;
            Log.Debug("Loading {CurrentUrl}", Driver.Url);
            Driver.Refresh();
            await Sleep(5000);
            // TODO: Also need to get account token for requests
        }
        catch (WebDriverException)
        {
            // Ignore
            Log.Warning("WebDriver unreachable, resetting...");
            WebDriver.RegenerateDriver();
        }
    }
    
    private async Task<bool> GoFileLogin()
    {
        var origUrl = Driver.Url;
        var loginLink = Config.Custom.GoFile.LoginLink;
        Driver.Url = loginLink;
        await Sleep(10000);
        for (var i = 0; i < RetryCount; i++)
        {
            await Sleep(2500);
            if (Driver.Url == "https://gofile.io/myProfile")
            {
                Log.Debug("Logged in to GoFile");
                break;
            }
            
            if (i == 3)
            {
                Log.Warning("Failed to login to GoFile: {CurrentUrl}", Driver.Url);
                #if DEBUG
                Driver.TakeDebugScreenshot("gofile.png");
                #endif
            }
        }
        
        Driver.Url = origUrl;
        return true;
    }

    private static async Task RenameFile(string src, string dst)
    {
        if (!File.Exists(dst))
        {
            File.Move(src, dst);
            return;
        }
        
        var srcHash = await FileUtility.GetFileHash(src);
        var dstHash = await FileUtility.GetFileHash(dst);
        if (srcHash.SequenceEqual(dstHash))
        {
            Log.Information("File already exists and is same, deleting src...");
            File.Delete(src);
        }
        else
        {
            var ext = Path.GetExtension(dst);
            var filename = Path.GetFileNameWithoutExtension(dst);
            var directory = Path.GetDirectoryName(dst)!;
            var newFilename = $"{filename} ({DateTime.Now:yyyy-MM-dd HH-mm-ss}){ext}";
            File.Move(src, Path.Combine(directory, newFilename));
            Log.Information("File already exists but is different, renaming src...");
        }
    }

    /// <summary>
    ///     Write response data to file
    /// </summary>
    /// <param name="response">Response to write to file</param>
    /// <param name="path">Filepath to write to</param>
    /// <param name="resumeFrom">Byte offset to resume from</param>
    /// <returns>Boolean based on successfulness</returns>
    private async Task<DownloadStatus> WriteToFile(HttpResponseMessage response, string path, long resumeFrom)
    {
        var expandedFilePath = path.StartsWith('~')
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..])
            : path;

        var idleTimeout = TimeSpan.FromSeconds(30); // 30 seconds idle timeout
        var savePath = Path.GetFullPath(expandedFilePath);
        try
        {
            return await BufferedWrite(response, savePath, idleTimeout, resumeFrom);
        }
        catch (HttpRequestException)
        {
            Log.Warning("Connection Reset, Retrying...");
            await Sleep(1000); // Wait for 1 second before retrying
            return DownloadStatus.ConnectionReset;
        }
    }

    private async Task<DownloadStatus> BufferedWrite(HttpResponseMessage response, string savePath, TimeSpan idleTimeout, long resumFrom)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
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
            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                totalSize += bytesRead;
                //Log.Debug("Downloaded {TotalSize} bytes...", totalSize);
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));

                if (DateTime.UtcNow - lastActivity > idleTimeout)
                {
                    Log.Warning("Download timed out due to inactivity.");
                    throw new DownloadTimeoutException(totalSize, "No data received for too long.");
                }
                
                lastActivity = DateTime.UtcNow;
            }
        }
        catch (IOException e)
        {
            if (e.Message.StartsWith("The response ended prematurely"))
            {
                Log.Warning("Download response ended prematurely.");
                throw new DownloadTimeoutException(totalSize, e.Message, e);
            }
            
            Log.Error("An IO error occured: {savePath} - Reason: {Reason}", savePath, e.Message);
            return DownloadStatus.Failed;
        }
            
        if (totalSize < MinimumFileSize)
        {
            Log.Warning("Downloaded file is very small: {FilePath} ({Size} bytes)", savePath, totalSize);
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
            using var archive = SevenZipArchive.Open(file);
            UncompressFile(file, archive);
        });
        count += intermediateCount;
        error += intermediateError;
        
        files = Directory.GetFiles(directoryPath, "*.rar", SearchOption.AllDirectories);
        (intermediateCount, intermediateError) = UncompressAndGetResults(files, file =>
        {
            using var archive = RarArchive.Open(file);
            UncompressFile(file, archive);
        });
        count += intermediateCount;
        error += intermediateError;
        
        downloadStats.ArchivesExtracted += count;
        downloadStats.ArchivesExtractionFailed += error;
    }

    private static (int, int) UncompressAndGetResults(string[] files, Action<string> uncompressAction)
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
                Log.Error("Failed to extract: {file}", file);
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
        Log.Error("Method not yet implemented...");
    }
    
    private bool CookiesNeeded()
    {
        return SiteName == "titsintops";
    }
    
    private byte[] GetImageViaSelenium(string url)
    {
        Driver.Url = url;
        var b64Img = (string?)Driver.ExecuteScript("""
                                                  var img = document.getElementsByTagName("img")[0];
                                                  var canvas = document.createElement("canvas");
                                                  canvas.width = img.naturalWidth;
                                                  canvas.height = img.naturalHeight;
                                                  var ctx = canvas.getContext("2d");
                                                  ctx.drawImage(img, 0, 0);
                                                  var dataURL = canvas.toDataURL("image/png");
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
    
    private static void PrintDebugInfo(string title, string fd = "output.txt", bool clear = false, params object[] data)
    {
        using var writer = new StreamWriter(fd, !clear, Encoding.Unicode);
        writer.WriteLine($"[{title}]");
        foreach (var d in data)
        {
            writer.WriteLine($"\t{d.ToString()?.Trim()}");
        }
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
    
    [GeneratedRegex(@"//c(\d)+")]
    private static partial Regex DotPartySubdomainRegex();
    [GeneratedRegex("//c\\d+")]
    private static partial Regex DotPartyReplacementRegex();
    [GeneratedRegex(@"<img.+src=""([^""]+)""")]
    private static partial Regex NLegsImageUrlRegex();
}