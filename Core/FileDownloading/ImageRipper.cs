using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
    private const string RipIndex = ".ripIndex";
    private const int RetryCount = 4;
    private const int MillisecondsInSecond = 1000;

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
    public bool Paused { get; set; }
    
    private bool _disposed;
    
    private FirefoxDriver Driver => WebDriver.Driver;

    private static GeneralConfig Config => Configuration.Config.Instance;
    private static TokenManager TokenManager => TokenManager.Instance;
    private static FlareSolverrManager FlareSolverrManager => NicheImageRipper.FlareSolverrManager;
    
    public event Action<int, int>? OnProgressChanged;

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
        if (File.Exists(RipIndex))
        {
            var index = await File.ReadAllTextAsync(RipIndex);
            start = int.Parse(index);
            File.Delete(RipIndex);
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
        
        var htmlParser = HtmlParser.GetParser(SiteName, WebDriver, RequestHeaders, FilenameScheme);
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
        var downloadStats = new DownloadStats();
        var filesHashes = new HashSet<HashKey>();
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
        
        if(((double)downloadStats.FailedDownloads) / FolderInfo.NumUrls > FailureThreshold)
        {
            var e = new RipperException("More than 50% of the images failed to download");
            Log.Error(e, "More than 50% of the images failed to download");
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

        var downloadResults = downloadStats.GetStats(FolderInfo.NumUrls);
        Log.Information(downloadResults); // This is done to avoid the enclosing quotes around the string
        Log.Information("Download Complete");
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

            while (Paused)
            {
                await Sleep(1000);
            }

            foreach (var (i, ext) in extensions.Enumerate())
            {
                try
                {
                    OnProgressChanged?.Invoke(index, FolderInfo.NumUrls);
                    var fullFilename = $"{index}{ext}";
                    var imagePath = Path.Combine(fullPath, fullFilename);
                    await DownloadFromUrl(imageLink, index.ToString(), imagePath, ext);
                    await PostProcess(imagePath, filesHashes, downloadStats);
                    break;
                }
                catch // TODO: Narrow down exceptions
                {
                    if (i == 3)
                    {
                        downloadStats.FailedDownloads++;
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
                while (true)
                {
                    try
                    {
                        foreach (var (i, link) in FolderInfo.Urls.Skip(start).Enumerate())
                        {
                            var index = start + i;
                            OnProgressChanged?.Invoke(index + 1, FolderInfo.NumUrls);
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
                                await DownloadFromList(link, imagePath, index, downloadStats);
                                await PostProcess(imagePath, filesHashes, downloadStats);
                            }
                            catch (FileNotFoundException)
                            {
                                if (link.LinkInfo == LinkInfo.IframeMedia)
                                {
                                    downloadStats.FailedDownloads++;
                                    await File.AppendAllTextAsync("failed_iframe.txt", $"{link.Url} {link.Referer}\n");
                                }
                            }
                            catch (EHentaiUrlExpiredException e)
                            {
                                e.ResumeIndex = index;
                                throw;
                            }
                            catch
                            {
                                await File.WriteAllTextAsync(".ripIndex", CurrentIndex.ToString());
                                throw;
                            }
                        }

                        break;
                    }
                    catch (EHentaiUrlExpiredException e)
                    {
                        var parser = new EHentaiParser(WebDriver, RequestHeaders, FilenameScheme);
                        start = e.ResumeIndex;
                        var updatedLinks = await parser.UpdateLinks(FolderInfo.Urls, start);
                        FolderInfo.Urls = updatedLinks;
                    }
                }

                break;
            }
        }
    }

    private async Task PostProcess(string imagePath, HashSet<HashKey> filesHashes, DownloadStats downloadStats)
    {
        if (PostDownloadAction.HasFlag(PostDownloadAction.RemoveDuplicates))
        {
            // Maybe handle directory downloads (e.g., Mega) in the future?
            if (Directory.Exists(imagePath))
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
        var exitCode = await RunSubprocess("gallery-dl", cmd, startMessage: "Starting Deviantart download",
            endMessage: "Deviantart download finished");
        if (exitCode != 0)
        {
            Log.Error("Failed to download from DeviantArt");
        }
        
        return exitCode == 0;
    }
    
    private static async Task<FfmpegStatusCode> RunFfmpeg(string[] cmd, string startMessage, string endMessage,
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
        var exitCode = await RunSubprocess("ffmpeg", cmd, captureError: displayOutput,
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
        var exitCode = await RunSubprocess("yt-dlp", cmd, /*true, true,*/ 
            startMessage: startMessage, endMessage: endMessage);
        if (exitCode != 0)
        {
            Log.Error("Failed to run yt-dlp: {ExitCode}", exitCode);
        }
        
        return exitCode == 0;
    }
    
    private static async Task<int> RunSubprocess(string executable, string[]? arguments = null,
                                     bool captureOutput = false, bool captureError = false,
                                     string? startMessage = null, string? endMessage = null)
    {
        if (startMessage is not null)
        {
            Log.Information(startMessage);
        }
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments is null ? "" : " ".Join(arguments),
                RedirectStandardOutput = captureOutput,
                RedirectStandardError = captureError,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        if (captureOutput)
        {
            process.OutputDataReceived += (_, args) => Log.Information(args.Data ?? "null");
        }
        
        if (captureError)
        {
            process.ErrorDataReceived += (_, args) => Log.Error(args.Data ?? "null");
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
            Log.Information(endMessage);
        }
        
        return exitCode;
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
    private async Task DownloadFromUrl(ImageLink imageLink, string filename, string imagePath, string ext)
    {
        var numFiles = FolderInfo.NumUrls;
        // Completes the specific image URL from the general URL
        var url = imageLink.Url;
        var fullFilename = $"{filename}{ext}";
        var ripUrl = $"{url}{fullFilename}";
        var numProgress = $"({filename}/{numFiles})";
        Log.Information($"{ripUrl}    {numProgress}");
        imageLink.Url = ripUrl;
        try
        {
            await DownloadFile(imagePath, imageLink, true);
        }
        finally
        {
            imageLink.Url = url;
        }
        await Sleep(50);
    }

    /// <summary>
    ///     Download images from url supplied from a list of image urls
    /// </summary>
    /// <param name="imageLink">ImageLink containing data on the file to download</param>
    /// <param name="imagePath">Full path of the location to save the file to</param>
    /// <param name="currentFileNum">Number of the file being downloaded</param>
    /// <param name="downloadStats">DownloadStats object to update with results</param>
    private async Task DownloadFromList(ImageLink imageLink, string imagePath, int currentFileNum,
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

        bool success;
        switch (imageLink.LinkInfo)
        {
            case LinkInfo.M3U8Ffmpeg:
                success = await DownloadM3U8ToMp4(imagePath, imageLink);
                break;
            case LinkInfo.M3U8YtDlp:
                success = await DownloadM3U8YtDlp(imagePath, imageLink);
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
            case LinkInfo.Youtube:
                success = await DownloadYoutubeFile(imagePath, imageLink);
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
            downloadStats.FailedDownloads++;
        }

        RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
        await Sleep(50);
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

    private static async Task<bool> DownloadMpegDashFile(string path, ImageLink imageLink)
    {
        var parent = Directory.GetParent(path)!.FullName;
        var filename = Path.GetFileName(path);
        var cmd = new[] { "-P", $"\"{parent}\"", imageLink.Url, "-o", filename };
        var exitCode = await RunSubprocess("yt-dlp", cmd, startMessage: "Starting youtube-dl download",
            endMessage: "youtube-dl download finished");
        return exitCode == 0;
    }

    private static async Task<bool> DownloadM3U8ToMp4(string path, ImageLink imageLink)
    {
        var url = imageLink.Url;
        var referer = imageLink.Referer;
        if (!path.Contains('.'))
        {
            if (url.Contains(".mp4"))
            {
                path += ".mp4";
            }
            else if (url.Contains(".webm"))
            {
                path += ".webm";
            }
            else
            {
                path += ".ts";
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
                $"\"{path}\""
            ];
        }
        else
        {
            cmd =
            [
                "-protocol_whitelist", "file,http,https,tcp,tls,crypto", 
                "-i", $"\"{url}\"",
                "-c", "copy",
                $"\"{path}\""
            ];
        }
        
        var result = await RunFfmpeg(cmd, "Starting ffmpeg download", "ffmpeg download finished");
        Log.Debug("Ffmpeg result: {Result}", result.GetShortErrorMessage());
        return result.IsSuccess();
    }

    private static Task<bool> DownloadM3U8YtDlp(string path, ImageLink imageLink)
    {
        return RunYtDlp(imageLink, path, startMessage: "Starting yt-dlp download",
            endMessage: "yt-dlp download finished");
    }
    
    private static async Task<bool> DownloadGDriveFile(string path, ImageLink imageLink)
    {
        var destinationPath = Path.Combine(path, imageLink.Filename);
        Directory.CreateDirectory(path);
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
    
    private static async Task<bool> DownloadIframeMedia(string folderPath, ImageLink imageLink)
    {
        var parentPathInfo = Directory.GetParent(folderPath)!;
        var parentPath = parentPathInfo.FullName; 
        Directory.CreateDirectory(parentPath);
        for(var i = 0; i < RetryCount; i++)
        {
            try
            {
                var video = new BunnyVideoDrm(
                    referer: imageLink.Url,
                    embedUrl: imageLink.Referer,
                    name: Path.GetFileName(folderPath).Split('.')[0],
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
    
    private async Task<bool> DownloadMegaFiles(string path, ImageLink imageLink)
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
            path = Path.GetDirectoryName(path)!;
        }
        else
        {
            Log.Debug("Downloading folder from Mega: {Url}", imageLink.Url);
            Directory.CreateDirectory(path);
        }

        while (true)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                return await MegaApi.DownloadAsync(imageLink.Url, path, cts.Token);
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
    
    private static async Task<bool> DownloadPixelDrainFiles(string path, ImageLink imageLink)
    {
        var apiKey = Config.Keys.Pixeldrain;
        var authString = $":{apiKey}";
        var base64Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
        var headers = new Dictionary<string, string>
        {
            [RequestHeaderKeys.UserAgent] =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/107.0.0.0 Safari/537.36",
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
        
        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fileStream);
        return true;
    }

    private static Task<bool> DownloadYoutubeFile(string path, ImageLink imageLink)
    {
        return RunYtDlp(imageLink, path, startMessage: "Starting youtube-dl download",
            endMessage: "youtube-dl download finished");
    }

    private async Task<bool> DownloadSeleniumImage(string path, ImageLink imageLink)
    {
        try
        {
            var imageData = GetImageViaSelenium(imageLink.Url);
            await File.WriteAllBytesAsync(path, imageData);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to download image");
            return false;
        }
    }
    
    private static async Task<bool> DownloadBase64Image(string path, ImageLink imageLink)
    {
        try
        {
            var base64Data = imageLink.Url.Split(',')[1];
            var imageData = Convert.FromBase64String(base64Data);
            await File.WriteAllBytesAsync(path, imageData);
            return true;
        }
        catch (FormatException e)
        {
            Log.Error(e, "Failed to decode base64 image");
            return false;
        }
    }
    
    private async Task<bool> DownloadFile(string imagePath, ImageLink imageLink, bool generatingManually)
    {
        if(imagePath[^1] == '/')
        {
            imagePath = imagePath[..^1];
        }

        try
        {
            var successful = false;
            for (var _ = 0; _ < RetryCount; _++)
            {
                var success = await DownloadFileHelper(imageLink, imagePath, generatingManually);
                if (success)
                {
                    successful = true;
                    break;
                }
            }

            if (!successful)
            {
                return false; // Failed to download file
            }
        }
        // If unable to download file due to multiple subdomains (e.g. data1, data2, etc.)
        // Context:
        //   https://c1.kemono.party/data/95/47/95477512bd8e042c01d63f5774cafd2690c29e5db71e5b2ea83881c5a8ff67ad.gif]
        //   will fail, however, changing the subdomain to c5 will allow requests to download the file
        //   given that there are correct cookies in place
        catch (BadSubdomainException)
        {
            await DotPartySubdomainHandler(imageLink.Url, imagePath);
        }
        
        // If the downloaded file doesn't have an extension for some reason, search for correct ext
        if (Path.GetExtension(imagePath) == "")
        {
            var extension = FileUtility.GetCorrectExtension(imagePath);
            RenameFile(imagePath, imagePath + extension);
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
        var modifiedHeader = ModifiedHeader.None;
        var oldCookies = "";
        if (url.Contains("redgifs"))
        {
            modifiedHeader = ModifiedHeader.Authorization;
            var token = await TokenManager.GetToken("redgifs");
            RequestHeaders[RequestHeaderKeys.Authorization] = $"Bearer {token.Value}";
        }
        else if (imageLink.LinkInfo == LinkInfo.GoFile)
        {
            modifiedHeader = ModifiedHeader.Cookie;
            var cookieValue = Config.Custom[ConfigKeys.CustomKeys.GoFile]["accountToken"];
            var cookie = $"accountToken={cookieValue}";
            oldCookies = RequestHeaders[RequestHeaderKeys.Cookie];
            RequestHeaders[RequestHeaderKeys.Cookie] = cookie;
        }
        else if (imageLink.Url.Contains("donmai.us"))
        {
            modifiedHeader = ModifiedHeader.UserAgent;
            RequestHeaders[RequestHeaderKeys.UserAgent] = "NicheImageRipper";
        }

        Log.Debug("Request Headers: {@RequestHeaders}", RequestHeaders);
        HttpResponseMessage response;
        try
        {
            using var request = RequestHeaders.ToRequest(HttpMethod.Get, url);
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
            Log.Warning("<Response {ResponseStatusCode}>", response.StatusCode);
            await Sleep(500);
            
            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                {
                    LogFailedUrl(url);
                    if (!generatingManually)
                    {
                        return false;
                    }

                    throw new WrongExtensionException();
                }
                case HttpStatusCode.Unauthorized:
                    return false;
                case HttpStatusCode.Forbidden:
                    switch (SiteName)
                    {
                        case "kemono" when !url.Contains(".psd"):
                            Log.Information("Wrong subdomain, trying again...");
                            throw new BadSubdomainException();
                        case "e-hentai":
                            Log.Information("E-Hentai URL expired, trying to update links...");
                            await Task.Delay(10 * MillisecondsInSecond); // Wait for 10 seconds before retrying
                            throw new EHentaiUrlExpiredException();
                    }
                    return false;
                case HttpStatusCode.BadGateway:
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
                case HttpStatusCode.InternalServerError:
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
                    break;
                
                #endregion
            }
        }

        var result = await WriteToFile(response, imagePath);
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

        if (modifiedHeader.HasFlag(ModifiedHeader.Authorization))
        {
            RequestHeaders.Remove(RequestHeaderKeys.Authorization);
        }
        else if (modifiedHeader.HasFlag(ModifiedHeader.Cookie))
        {
            // Add more logic here if other sites require cookies when downloading files
            //RequestHeaders[RequestHeaderKeys.Cookie] = GoFileAccountTokenCookieRegex().Replace(RequestHeaders[RequestHeaderKeys.Cookie], "");
            RequestHeaders[RequestHeaderKeys.Cookie] = oldCookies;
        }
        else if (modifiedHeader.HasFlag(ModifiedHeader.UserAgent))
        {
            RequestHeaders[RequestHeaderKeys.UserAgent] = Config.UserAgent;
        }

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
        var loginLink = Config.Custom[ConfigKeys.CustomKeys.GoFile]["loginLink"];
        Driver.Url = loginLink;
        await Sleep(10000);
        for (var i = 0; i < 4; i++)
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
                Driver.GetScreenshot().SaveAsFile("test2.png");
            }
        }
        
        Driver.Url = origUrl;
        return true;
    }

    private static void RenameFile(string src, string dst)
    {
        if (!File.Exists(dst))
        {
            File.Move(src, dst);
            return;
        }
        
        var srcHash = HashFile(src);
        var dstHash = HashFile(dst);
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
    
    private static byte[] HashFile(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha256.ComputeHash(stream);
        return hash;
    }
    
    private async Task DotPartySubdomainHandler(string url, string imagePath)
    {
        var subdomainSearch = DotPartySubdomainRegex().Match(url);
        if (!subdomainSearch.Success)
        {
            PrintDebugInfo("bad_subdomain", url);
            throw new ImproperlyFormattedSubdomainException();
        }
        
        var subdomainNum = int.Parse(subdomainSearch.Groups[1].Value);
        for (var i = 0; i < 100; i++)
        {
            if (i == subdomainNum)
            {
                continue;
            }
            
            var ripUrl = DotPartyReplacementRegex().Replace(url, $"//c{i}");
            
            try
            {
                await DownloadPartyFile(imagePath, ripUrl);
            }
            catch (BadSubdomainException)
            {
                Log.Information("Trying subdomain c{SubdomainIndex}...", i);
                if (i == 99)
                {
                    LogFailedUrl(DotPartyReplacementRegex().Replace(url, $"//c{subdomainNum}"));
                    return;
                }
            }
        }
        
        Log.Information(url);
    }

    private async Task DownloadPartyFile(string imagePath, string ripUrl)
    {
        await Task.Delay((int)(SleepTime * MillisecondsInSecond));

        for(var _ = 0; _ < RetryCount; _++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = RequestHeaders.ToRequest(HttpMethod.Get, ripUrl);
                response = await Session.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (HttpRequestException)
            {
                Log.Error("Unable to establish connection to {url}", ripUrl);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Forbidden && !ripUrl.Contains(".psd"))
                {
                    throw new BadSubdomainException();
                }

                Log.Warning("<Response {statusCode}>", response.StatusCode);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    LogFailedUrl(ripUrl);
                    throw new FileNotFoundAtUrlException(ripUrl);
                }
            }

            var result = await WriteToFile(response, imagePath);
            switch (result)
            {
                case DownloadStatus.None:
                    // Should not occur
                    break;
                case DownloadStatus.Ok:
                    goto BreakLoop;
                case DownloadStatus.ConnectionReset:
                    continue;
                case DownloadStatus.Failed:
                    LogFailedUrl(ripUrl);
                    goto BreakLoop;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        BreakLoop: ;
    }
    
    /// <summary>
    ///     Write response data to file
    /// </summary>
    /// <param name="response">Response to write to file</param>
    /// <param name="path">Filepath to write to</param>
    /// <returns>Boolean based on successfulness</returns>
    private async Task<DownloadStatus> WriteToFile(HttpResponseMessage response, string path)
    {
        var expandedFilePath = path.StartsWith('~')
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..])
            : path;

        var idleTimeout = TimeSpan.FromSeconds(30); // 30 seconds idle timeout
        var savePath = Path.GetFullPath(expandedFilePath);
        const int minimumFileSize = 1024; // 1KB minimum file size
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream =
                new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[4096]; // 4KB buffer
            int bytesRead;
            var totalSize = 0L;
            var lastActivity = DateTime.UtcNow;

            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                totalSize += bytesRead;
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));

                if (DateTime.UtcNow - lastActivity > idleTimeout)
                {
                    throw new TimeoutException("No data received for too long.");
                }
                
                lastActivity = DateTime.UtcNow;
            }
            
            if (totalSize < minimumFileSize)
            {
                Log.Warning("Downloaded file is very small: {FilePath} ({Size} bytes)", savePath, totalSize);
                if (SiteName == "e-hentai")
                {
                    throw new EHentaiUrlExpiredException();
                }

                return DownloadStatus.Failed;
            }
            
            return DownloadStatus.Ok; // Success
        }
        catch (HttpRequestException)
        {
            Log.Warning("Connection Reset, Retrying...");
            await Sleep(1000); // Wait for 1 second before retrying
            return DownloadStatus.ConnectionReset;
        }
        catch (IOException)
        {
            Log.Error("Failed to open file: {savePath}", savePath);
            return DownloadStatus.Failed; // No retry on file operation errors
        }
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
        downloadStats.FailedDownloads += error;
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