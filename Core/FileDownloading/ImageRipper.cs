using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.History;
using Core.Managers;
using Core.SiteParsing;
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
using WebDriver = Core.History.WebDriver;

namespace Core.FileDownloading;

public partial class ImageRipper : IDisposable
{
    private const string RipIndex = ".ripIndex";
    private const int RetryCount = 4;

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
    public bool LoggedIn { get; set; }
    private Dictionary<string, bool> PersistentLogins { get; }
    private string SavePath { get; }
    private HttpClient Session { get; }
    private string SiteName { get; set; }
    private float SleepTime { get; set; }
    public int CurrentIndex { get; private set; }
    private WebDriverPool DriverPool { get; }
    private WebDriver WebDriver { get; }
    
    private FirefoxDriver Driver => WebDriver.Driver;

    private static Config Config => Config.Instance;
    private static TokenManager TokenManager => TokenManager.Instance;
    private static FlareSolverrManager FlareSolverrManager => NicheImageRipper.FlareSolverrManager;

    public ImageRipper(WebDriverPool driverPool, FilenameScheme filenameScheme = FilenameScheme.Original,
                       UnzipProtocol unzipProtocol = UnzipProtocol.None, PostDownloadAction postDownloadAction = PostDownloadAction.None)
    {
        FilenameScheme = filenameScheme;
        UnzipProtocol = unzipProtocol;
        PostDownloadAction = postDownloadAction;
        //FolderInfo = null;
        GivenUrl = "";
        Interrupted = false;
        LoggedIn = File.Exists("cookies.pkl");
        PersistentLogins = new Dictionary<string, bool>();
        // Create copy of save path, so that config can be changed without affecting current ripper instance
        SavePath = Config.SavePath;
        Session = new HttpClient();
        SiteName = "";
        SleepTime = 0.2f;
        CurrentIndex = 0;
        DriverPool = driverPool;
        WebDriver = driverPool.AcquireDriver(false);
    }

    public async Task Rip(string url)
    {
        SleepTime = 0.2f;   // Reset sleep time
        GivenUrl = url.Replace("members.", "www."); // Replace is done to properly parse hanime pages
        (SiteName, SleepTime) = UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        if (CookiesNeeded())
        {
            AddCookies();
        }

        await FileGetter();
    }

    private async Task FileGetter()
    {
        var htmlParser = new HtmlParser(WebDriver, RequestHeaders, SiteName, FilenameScheme);
        FolderInfo = await htmlParser.ParseSite(GivenUrl);
        Log.Debug("Folder Info: {@FolderInfo}", FolderInfo);
        //Log.Debug("Directory Name: {DirectoryName}", FolderInfo.DirectoryName);
        var fullPath = Path.Combine(SavePath, FolderInfo.DirectoryName);
        if (Interrupted && FilenameScheme != FilenameScheme.Hash)
        {
            // TODO: self.folder_info.urls = self.get_incomplete_files(full_path)
        }
        
        Directory.CreateDirectory(fullPath);

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

        var downloadStats = new DownloadStats();
        var filesHashes = new HashSet<HashKey>();
        // Can get the image through numerically ascending url for imhentai and hentairox
        //   (hard to account for gifs and other extensions otherwise)
        if (FolderInfo.MustGenerateManually)
        {
            // Gets the general url for all images in this album
            var imageLink = FolderInfo.Urls[0];
            var trimmedUrl = TrimUrl(imageLink.Url);
            imageLink.Url = trimmedUrl;
            string[] extensions = [".jpg", ".gif", ".png", ".webp", ".webm", ".mp4", "t.jpg"];
            
            // Downloads all images from the general url by incrementing the file number
            //  (e.g., https://domain/gallery/##.jpg)
            for (var index = start; index < FolderInfo.NumUrls + 1; index++)
            {
                CurrentIndex = index;
                
                // while(pause) { sleep(1); }

                foreach (var (i, ext) in extensions.Enumerate())
                {
                    try
                    {
                        var fullFilename = $"{index}{ext}";
                        var imagePath = Path.Combine(fullPath, fullFilename);
                        await DownloadFromUrl(imageLink, index.ToString(), imagePath, ext);
                        if (PostDownloadAction.HasFlag(PostDownloadAction.RemoveDuplicates))
                        {
                            await HandleDuplicateFile(imagePath, filesHashes);
                        }
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
        // Easier to put all image url in a list and then download for these sites
        else
        {
            switch (SiteName)
            {
                case "deviantart":
                    await DeviantArtDownload(fullPath, FolderInfo.Urls[0].Url);
                    break;
                default:
                {
                    foreach (var (i, link) in FolderInfo.Urls[start..].Enumerate())
                    {
                        var index = start + i;
                        CurrentIndex = index;
                        // while(pause) { sleep(1); }
                        await Task.Delay((int) SleepTime * 1000);
                        try
                        {
                            var filename = link.Filename;
                            var imagePath = Path.Combine(fullPath, filename);
                            await DownloadFromList(link, imagePath, index, downloadStats);
                            if (PostDownloadAction.HasFlag(PostDownloadAction.RemoveDuplicates))
                            {
                                await HandleDuplicateFile(imagePath, filesHashes);
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            if (link.LinkInfo == LinkInfo.IframeMedia)
                            {
                                downloadStats.FailedDownloads++;
                                await File.AppendAllTextAsync("failed_iframe.txt", $"{link.Url} {link.Referer}\n");
                            }
                        }
                        catch
                        {
                            await File.WriteAllTextAsync(".ripIndex", CurrentIndex.ToString());
                            throw;
                        }
                    }

                    break;
                }
            }
        }
        
        if(((double)downloadStats.FailedDownloads) / FolderInfo.NumUrls > 0.5)
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

        var downloadResults = downloadStats.GetStats();
        Log.Information(downloadResults);
        Log.Information("Download Complete"); //{#00FF00}
    }
    
    private static async Task<bool> DeviantArtDownload(string fullPath, string url)
    {
        var cmd = new []{"-D", $"\"{fullPath}\"", "-u", Config.Logins["DeviantArt"].Username, "-p", 
            Config.Logins["DeviantArt"].Password, "--write-log", "log.txt", url};
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

    private static async Task<bool> RunYtDlp(string url, string path, string startMessage, string endMessage)
    {
        var parent = Directory.GetParent(path)!.FullName;
        var filename = Path.GetFileName(path);
        string[] cmd = 
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

    private static async Task HandleDuplicateFile(string imagePath, HashSet<HashKey> filesHashes)
    {
        var fileHash = await FileUtility.GetFileHash(imagePath);
        if (!filesHashes.Add(fileHash))
        {
            Log.Information("Duplicate file detected: {ImagePath}", imagePath);
            File.Delete(imagePath);
        }
        else
        {
            Log.Debug("File hash: {FileHash}", fileHash);
        }
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
        await DownloadFile(imagePath, imageLink, true);
        await Task.Delay(50);
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
        var numProgress = $"({currentFileNum + 1}/{numFiles})";
        Log.Information($"{ripUrl}    {numProgress}");
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
        await Task.Delay(50);
    }

    private async Task<bool> ResolveAndDownloadFile(string path, ImageLink imageLink)
    {
        var url = imageLink.Url;
        for (var i = 0; i < RetryCount; i++)
        {
            var imageUrl = await GetDownloadUrl(url);
            if (imageUrl == "")
            {
            
                await Task.Delay(500);
                continue;
            }
            
            Log.Debug("Resolved URL: {Url}", imageUrl);
            imageLink.Url = imageUrl;

            var success = await DownloadFile(path, imageLink, false);
            if (success)
            {
                return true;
            }

            await Task.Delay(500);
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
        return RunYtDlp(imageLink.Url, path, startMessage: "Starting yt-dlp download",
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
    
    private Task<bool> DownloadMegaFiles(string path, ImageLink imageLink)
    {
        var (email, password) = Config.Instance.Logins["Mega"];
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
            throw new Exception("Unable to login to MegaCmd");
        }

        if (imageLink.Url.Contains("/file/"))
        {
            path = Path.GetDirectoryName(path)!;
        }
        else
        {
            Directory.CreateDirectory(path);
        }
        
        return Task.FromResult(MegaApi.Download(imageLink.Url, path));
    }
    
    private static async Task<bool> DownloadPixelDrainFiles(string path, ImageLink imageLink)
    {
        var apiKey = Config.Instance.Keys["Pixeldrain"];
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
        return RunYtDlp(imageLink.Url, path, startMessage: "Starting youtube-dl download",
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
        var url = imageLink.Url;
        var badCert = false;
        await Task.Delay((int)(SleepTime * 1000));
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
        else if (imageLink.Referer.Contains("donmai.us"))
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
        catch (HttpRequestException e) when (e.InnerException is AuthenticationException)
        {
            try
            {
                // TODO: See if this can be removed
                using var request = RequestHeaders.ToRequest(HttpMethod.Get, url);
                using var insecureClient = new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                });
                response = await insecureClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                badCert = true;
                Log.Warning("Bad certificate detected: {Url}", url);
            }
            catch (HttpRequestException e2) when (e2.InnerException is AuthenticationException)
            {
                return false;
            }
        }
        catch (HttpRequestException e) when (e.InnerException is InvalidOperationException)
        {
            Log.Error($"Unable to establish a connection to {url}");
            return false;
        }
        catch (HttpRequestException e) when (e.InnerException is SocketException)
        {
            Log.Error($"Unable to establish a connection to {url}");
            return false;
        }

        if (!response.IsSuccessStatusCode && !badCert)
        {
            if(response.StatusCode == HttpStatusCode.Forbidden && SiteName == "kemono" && !url.Contains(".psd"))
            {
                Log.Information("Wrong subdomain, trying again...");
                throw new BadSubdomainException();
            }

            Log.Warning($"<Response {response.StatusCode}>");
            await Task.Delay(500);
            
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
            await Task.Delay(5000);
            // TODO: Also need to get account token for requests
        }
        catch (WebDriverException)
        {
            // Ignore
            Log.Warning("WebDriver unreachable, resetting...");
            WebDriver.RegenerateDriver(false);
        }
    }
    
    private async Task<bool> GoFileLogin()
    {
        var origUrl = Driver.Url;
        var loginLink = Config.Custom[ConfigKeys.CustomKeys.GoFile]["loginLink"];
        Driver.Url = loginLink;
        await Task.Delay(10000);
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(2500);
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
        await Task.Delay((int)(SleepTime * 1000));

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
    private static async Task<DownloadStatus> WriteToFile(HttpResponseMessage response, string path)
    {
        var expandedFilePath = path.StartsWith('~')
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..])
            : path;

        var savePath = Path.GetFullPath(expandedFilePath);
        try
        {
            await using var fileStream =
                new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);
            return DownloadStatus.Ok; // Success
        }
        catch (HttpRequestException)
        {
            Log.Warning("Connection Reset, Retrying...");
            await Task.Delay(1000); // Wait for 1 second before retrying
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
        var b64Img = (string)Driver.ExecuteScript("""
                                                  var img = document.getElementsByTagName("img")[0];
                                                  var canvas = document.createElement("canvas");
                                                  canvas.width = img.naturalWidth;
                                                  canvas.height = img.naturalHeight;
                                                  var ctx = canvas.getContext("2d");
                                                  ctx.drawImage(img, 0, 0);
                                                  var dataURL = canvas.toDataURL("image/png");
                                                  return dataURL.replace(/^data:image\/(png|jpg);base64,/, "");
                                                  """);
        var bytes = Convert.FromBase64String(b64Img);
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
        Session.Dispose();
        DriverPool.ReleaseDriver(WebDriver);
        GC.SuppressFinalize(this);
    }
    
    [GeneratedRegex(@"//c(\d)+")]
    private static partial Regex DotPartySubdomainRegex();
    [GeneratedRegex("//c\\d+")]
    private static partial Regex DotPartyReplacementRegex();
    [GeneratedRegex(@"<img.+src=""([^""]+)""")]
    private static partial Regex NLegsImageUrlRegex();
}