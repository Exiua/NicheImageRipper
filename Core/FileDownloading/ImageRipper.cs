using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.SiteParsing;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace Core.FileDownloading;

public partial class ImageRipper
{
    private const string RipIndex = ".ripIndex";
    private const int RetryCount = 4;

    private static readonly string[] CyberdropDomains = ["cyberdrop.me", "cyberdrop.cc", "cyberdrop.to", "cyberdrop.nl"];

    private static readonly Dictionary<ulong, string> FileSignatures = new()
    {
        [0x89_50_4E_47_0D_0A_1A_0A] = ".png",   // /8
        [0x43_53_46_43_48_55_4E_4B] = ".clip",  // /8
        [0x3C_21_44_4F_43_54_59_50] = ".html",  // /8
        [0x52_61_72_21_1A_07_00_00] = ".rar",   // /6
        [0x37_7A_BC_AF_27_1C_00_00] = ".7z",    // /6
        [0x47_49_46_38_00_00_00_00] = ".gif",   // /4
        [0x50_4B_03_04_00_00_00_00] = ".zip",   // /4
        [0x38_42_50_53_00_00_00_00] = ".psd",   // /4
        [0x25_50_44_46_00_00_00_00] = ".pdf",   // /4
        [0x1A_45_DF_A3_00_00_00_00] = ".webm",  // /4
        [0x52_49_46_46_00_00_00_00] = ".webp",  // /4
        [0x00_00_00_00_66_74_79_70] = ".mp4",   // /4 reverse
        [0xFF_D8_FF_00_00_00_00_00] = ".jpg",   // /3
    };
    
    public Dictionary<string, string> RequestHeaders { get; set; } = new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36",
        ["referer"] = "https://imhentai.xxx/",
        ["cookie"] = "",
        //["Authorization"] = ""
    };
    
    public FilenameScheme FilenameScheme { get; set; }
    public UnzipProtocol UnzipProtocol { get; set; }
    public RipInfo FolderInfo { get; set; } = null!;
    public string GivenUrl { get; set; }
    public bool Interrupted { get; set; }
    public bool AutoExtract { get; set; }
    public Dictionary<string, Credentials> Logins { get; set; }
    public bool LoggedIn { get; set; }
    public Dictionary<string, bool> PersistentLogins { get; set; }
    public string SavePath { get; set; }
    public HttpClient Session { get; set; }
    public string SiteName { get; set; }
    public float SleepTime { get; set; }
    public int CurrentIndex { get; set; }

    public ImageRipper(FilenameScheme filenameScheme = FilenameScheme.Original, UnzipProtocol unzipProtocol = UnzipProtocol.None)
    {
        FilenameScheme = filenameScheme;
        UnzipProtocol = unzipProtocol;
        //FolderInfo = null;
        GivenUrl = "";
        Interrupted = false;
        AutoExtract = false;
        Logins = Config.Instance.Logins;
        LoggedIn = File.Exists("cookies.pkl");
        PersistentLogins = new Dictionary<string, bool>();
        SavePath = Config.Instance.SavePath;
        Session = new HttpClient();
        SiteName = "";
        SleepTime = 0.2f;
        CurrentIndex = 0;
    }

    public async Task Rip(string url)
    {
        SleepTime = 0.2f;   // Reset sleep time
        GivenUrl = url.Replace("members.", "www."); // Replace is done to properly parse hanime pages
        (SiteName, SleepTime) = Utility.UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        if (CookiesNeeded())
        {
            AddCookies();
        }

        await FileGetter();
    }

    private async Task FileGetter()
    {
        var htmlParser = new HtmlParser(RequestHeaders, SiteName, FilenameScheme);
        FolderInfo = await htmlParser.ParseSite(GivenUrl);
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
        // Can get the image through numerically ascending url for imhentai and hentairox
        //   (hard to account for gifs and other extensions otherwise)
        if (FolderInfo.MustGenerateManually)
        {
            // Gets the general url for all images in this album
            var trimmedUrl = TrimUrl(FolderInfo.Urls[0].Url);
            string[] extensions = [".jpg", ".gif", ".png", ".webp", ".webm", ".mp4", "t.jpg"];
            
            // Downloads all images from the general url by incrementing the file number
            //  (eg. https://domain/gallery/##.jpg)
            for (var index = start; index < FolderInfo.NumUrls + 1; index++)
            {
                CurrentIndex = index;
                
                // while(pause) { sleep(1); }

                foreach (var (i, ext) in extensions.Enumerate())
                {
                    try
                    {
                        await DownloadFromUrl(trimmedUrl, index.ToString(), fullPath, ext, downloadStats);
                        break;
                    }
                    catch // TODO: Narrow down exceptions
                    {
                        if (i == 3)
                        {
                            downloadStats.FailedDownloads++;
                            Console.WriteLine("Image not found");
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
                    DeviantArtDownload(fullPath, FolderInfo.Urls[0].Url);
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
                            await DownloadFromList(link, fullPath, index, downloadStats);
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
        
        if(UnzipProtocol != UnzipProtocol.None)
        {
            UnzipFiles(fullPath, downloadStats);
        }

        var downloadResults = downloadStats.GetStats();
        Console.WriteLine(downloadResults);
        Console.WriteLine("Download Complete"); //{#00FF00}
    }

    // private static void CyberdropDownload(string path, ImageLink cyberdropFile) => CyberdropDownload(path, [cyberdropFile]);
    //
    // private static void CyberdropDownload(string path, List<ImageLink> cyberdropFiles)
    // {
    //     var cmd = new List<string> {"gallery-dl", "-D", path};
    //     cmd.AddRange(cyberdropFiles.Select(file => file.Url));
    //     Console.WriteLine(cmd);
    //     RunSubprocess(cmd, "Starting gallery-dl", "Gallery-dl finished");
    // }
    
    private void DeviantArtDownload(string fullPath, string url)
    {
        var cmd = new []{"-D", $"\"{fullPath}\"", "-u", Logins["DeviantArt"].Username, "-p", 
            Logins["DeviantArt"].Password, "--write-log", "log.txt", url};
        var (exitCode, _, _) = RunSubprocess("gallery-dl", cmd, startMessage: "Starting Deviantart download",
            endMessage: "Deviantart download finished");
        if (exitCode != 0)
        {
            Console.WriteLine("Failed to download from DeviantArt");
        }
    }
    
    private static void RunFfmpeg(string[] cmd, string startMessage, string endMessage)
    {
        cmd = [ "-loglevel", "quiet", "-y", ..cmd ];
        var (exitCode, _, _) = RunSubprocess("ffmpeg", cmd, true, true, startMessage: startMessage, endMessage: endMessage);
        if (exitCode != 0)
        {
            Console.WriteLine($"Failed to run ffmpeg: {exitCode}");
        }
    }

    private static (int, string, string) RunSubprocess(string executable, string[]? arguments = null,
                                                       bool captureOutput = false, bool captureError = false,
                                                       string? startMessage = null, string? endMessage = null)
    {
        if (startMessage is not null)
        {
            Console.WriteLine(startMessage);
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
        
        process.Start();
        process.WaitForExit();
        var exitCode = process.ExitCode;
        var output = captureOutput ? process.StandardOutput.ReadToEnd() : "";
        var error = captureError ? process.StandardError.ReadToEnd() : "";
        if (endMessage is not null)
        {
            Console.WriteLine(endMessage);
        }
        
        return (exitCode, output, error);
    }

    /// <summary>
    ///     Download image from image url
    /// </summary>
    /// <param name="url">Base url to use to download the file from</param>
    /// <param name="filename">Name of the file to download</param>
    /// <param name="fullPath">Full path of the directory to download the file to</param>
    /// <param name="ext">Extension of the file to download</param>
    /// <param name="downloadStats">DownloadStats object to update with results</param>
    private async Task DownloadFromUrl(string url, string filename, string fullPath, string ext, DownloadStats downloadStats)
    {
        var numFiles = FolderInfo.NumUrls;
        // Completes the specific image URL from the general URL
        var ripUrl = $"{url}{filename}{ext}";
        var numProgress = $"({filename}/{numFiles})";
        Console.WriteLine($"{ripUrl}    {numProgress}");
        var imagePath = Path.Combine(fullPath, $"{filename}{ext}");
        await DownloadFile(imagePath, ripUrl, downloadStats, true);
        await Task.Delay(50);
    }

    /// <summary>
    ///     Download images from url supplied from a list of image urls
    /// </summary>
    /// <param name="imageLink">ImageLink containing data on the file to download</param>
    /// <param name="fullPath">Full path of the directory to save the file to</param>
    /// <param name="currentFileNum">Number of the file being downloaded</param>
    /// <param name="downloadStats">DownloadStats object to update with results</param>
    private async Task DownloadFromList(ImageLink imageLink, string fullPath, int currentFileNum, DownloadStats downloadStats)
    {
        var numFiles = FolderInfo.NumUrls;
        var ripUrl = imageLink.Url;
        var numProgress = $"({currentFileNum + 1}/{numFiles})";
        Console.WriteLine($"{ripUrl}    {numProgress}");
        var filename = imageLink.Filename;
        var imagePath = Path.Combine(fullPath, filename);
        switch (imageLink.LinkInfo)
        {
            case LinkInfo.M3U8:
                await DownloadM3U8ToMp4(imagePath, ripUrl);
                break;
            case LinkInfo.GDrive:
                await DownloadGDriveFile(imagePath, imageLink);
                break;
            case LinkInfo.IframeMedia:
                await DownloadIframeMedia(imagePath, imageLink);
                // TODO: Figure out how to delete temp directories
                break;
            case LinkInfo.Mega:
                await DownloadMegaFiles(imagePath, imageLink);
                break;
            case LinkInfo.PixelDrain:
                await DownloadPixelDrainFiles(imagePath, imageLink);
                break;
            case LinkInfo.Youtube:
                await DownloadYoutubeFile(imagePath, imageLink);
                break;
            case LinkInfo.None:
            default:
                await DownloadFile(imagePath, ripUrl, downloadStats, false);
                break;
        }
        await Task.Delay(50);
    }
    
    private static Task DownloadM3U8ToMp4(string path, string url)
    {
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
        
        var cmd = new []{"-protocol_whitelist", "file,http,https,tcp,tls,crypto", 
            "-i", url, "-c", "copy", $"\"{path}\""
        };
        RunFfmpeg(cmd, "Starting ffmpeg download", "ffmpeg download finished");
        return Task.CompletedTask;
    }

    private static async Task DownloadGDriveFile(string path, ImageLink imageLink)
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
    }
    
    private static async Task DownloadIframeMedia(string folderPath, ImageLink imageLink)
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
                }
            }
        }
    }
    
    private Task DownloadMegaFiles(string path, ImageLink imageLink)
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
        MegaApi.Download(imageLink.Url, path);
        return Task.CompletedTask;
    }
    
    private static async Task DownloadPixelDrainFiles(string path, ImageLink imageLink)
    {
        var apiKey = Config.Instance.Keys["Pixeldrain"];
        var authString = $":{apiKey}";
        var base64Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
        var headers = new Dictionary<string, string>
        {
            ["User-Agent"] =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/107.0.0.0 Safari/537.36",
            ["Authorization"] = $"Basic {base64Auth}"
        };
        using var client = new HttpClient();
        foreach (var (key, value) in headers)
        {
            client.DefaultRequestHeaders.Add(key, value);
        }
        var response = await client.GetAsync($"https://pixeldrain.com/api/file/{imageLink.Url}", HttpCompletionOption.ResponseHeadersRead);
        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fileStream);
    }

    private static Task DownloadYoutubeFile(string path, ImageLink url)
    {
        Directory.CreateDirectory(path);
        var cmd = new[] { "-P", $"\"{path}\"", url.Url };
        RunSubprocess("yt-dlp", cmd, startMessage: "Starting youtube-dl download",
            endMessage: "youtube-dl download finished");
        return Task.CompletedTask;
    }
    
    private async Task DownloadFile(string imagePath, string url, DownloadStats downloadStats, bool generatingManually)
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
                if (await DownloadFileHelper(url, imagePath, generatingManually))
                {
                    successful = true;
                    break;
                }
            }

            if (!successful)
            {
                downloadStats.FailedDownloads++;
                return; // Failed to download file
            }
        }
        // If unable to download file due to multiple subdomains (e.g. data1, data2, etc.)
        // Context:
        //   https://c1.kemono.party/data/95/47/95477512bd8e042c01d63f5774cafd2690c29e5db71e5b2ea83881c5a8ff67ad.gif]
        //   will fail, however, changing the subdomain to c5 will allow requests to download the file
        //   given that there are correct cookies in place
        catch (BadSubdomainException)
        {
            await DotPartySubdomainHandler(url, imagePath);
        }
        
        // If the downloaded file doesn't have an extension for some reason, search for correct ext
        if (Path.GetExtension(imagePath) == "")
        {
            var extension = GetCorrectExtension(imagePath);
            File.Move(imagePath, imagePath + extension);
        }
    }

    private async Task<bool> DownloadFileHelper(string url, string imagePath, bool generatingManually)
    {
        var badCert = false;
        await Task.Delay((int)(SleepTime * 1000));
        var tokenNeeded = url.Contains("redgifs");
        if (tokenNeeded)
        {
            var token = await TokenManager.Instance.GetToken("redgifs");
            RequestHeaders["Authorization"] = $"Bearer {token.Value}";
        }

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
                using var request = RequestHeaders.ToRequest(HttpMethod.Get, url);
                using var insecureClient = new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                });
                response = await insecureClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                badCert = true;
            }
            catch (HttpRequestException e2) when (e2.InnerException is AuthenticationException)
            {
                return false;
            }
        }
        catch (HttpRequestException e) when (e.InnerException is InvalidOperationException)
        {
            Console.WriteLine($"Unable to establish a connection to {url}");
            return false;
        }

        if (!response.IsSuccessStatusCode && !badCert)
        {
            if(response.StatusCode == HttpStatusCode.Forbidden && SiteName == "kemono" && !url.Contains(".psd"))
            {
                throw new BadSubdomainException();
            }

            Console.WriteLine($"<Response {response.StatusCode}>");
            
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
                    await Task.Delay(500);
                    return false;
                case HttpStatusCode.Forbidden:
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
                case HttpStatusCode.BadGateway:
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

        if (tokenNeeded)
        {
            RequestHeaders.Remove("Authorization");
        }

        return true;
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
                Console.Write($"\rTrying subdomain c{i}...");
                if (i == 99)
                {
                    LogFailedUrl(DotPartyReplacementRegex().Replace(url, $"//c{subdomainNum}"));
                    return;
                }
            }
        }
        
        Console.WriteLine(url);
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
                Console.WriteLine($"Unable to establish connection to {ripUrl}");
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Forbidden && !ripUrl.Contains(".psd"))
                {
                    throw new BadSubdomainException();
                }

                Console.WriteLine($"<Response {response.StatusCode}>");

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
            Console.WriteLine("Connection Reset, Retrying...");
            await Task.Delay(1000); // Wait for 1 second before retrying
            return DownloadStatus.ConnectionReset;
        }
        catch (IOException)
        {
            Console.WriteLine($"Failed to open file: {savePath}");
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
        var files = Directory.GetFiles(directoryPath, "*.zip", SearchOption.AllDirectories);
        var count = 0;
        var error = 0;
        foreach (var file in files)
        {
            try
            {
                UnzipFile(file);
                count++;
            }
            catch (Exception)
            {
                Console.WriteLine($"Failed to extract: {file}");
                error++;
            }
        }
        
        downloadStats.ArchivesExtracted += count;
        downloadStats.FailedDownloads += error;
        //Console.WriteLine($"Archive Results:\n\tExtracted: {count}\n\tFailed: {error}");
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
            var ext = GetCorrectExtension(zipPath);
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
    ///     Get correct extension for a file based on file signature
    /// </summary>
    /// <param name="filepath">Path to the file to analyze</param>
    /// <returns>True extension of the file or the original extension if the file signature is unknown (will default to
    ///     .bin if the file does not have a file extension)</returns>
    public static string GetCorrectExtension(string filepath)
    {
        var signature = ReadSignature(filepath, 8);
        if (signature is null)
        {
            return ".bin"; // Default extension if reading failed or file is too small
        }

        var signatureInt = ToUInt64(signature, 0);
        ulong[] masks =
        [
            0xFFFF_FFFF_FFFF_FFFF, // 8 bytes
            0xFFFF_FFFF_FFFF_0000, // 6 bytes
            0xFFFF_FFFF_0000_0000, // 4 bytes
            0x0000_0000_FFFF_FFFF, // 4 bytes reverse
            0xFFFF_FF00_0000_0000, // 3 bytes
        ];

        foreach (var mask in masks)
        {
            var maskedSignature = signatureInt & mask;
            if (FileSignatures.TryGetValue(maskedSignature, out var ext))
            {
                return ext; // Return the extension if a matching signature is found
            }
        }

        return ".bin"; // Default extension if no matching signature is found
    }
    
    private static ulong ToUInt64(byte[] bytes, int startIndex)
    {
        var value = 0UL;
        for (var i = 0; i < 8; i++)
        {
            value |= (ulong)bytes[startIndex + i] << (8 * (7 - i));
        }
        
        return value;
    }

    private static byte[]? ReadSignature(string filepath, int length)
    {
        var signature = new byte[length];
        using var stream = File.OpenRead(filepath);
        var bytesRead = stream.Read(signature, 0, length);
        // Return null if the file is shorter than the required signature length
        return bytesRead < length ? null : signature;
            
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
        Console.Error.WriteLine("Method not yet implemented...");
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
    
    private static void PrintDebugInfo(string title, string fd = "output.txt", bool clear = false, params object[] data)
    {
        using var writer = new StreamWriter(fd, !clear, Encoding.Unicode);
        writer.WriteLine($"[{title}]");
        foreach (var d in data)
        {
            writer.WriteLine($"\t{d.ToString()?.Trim()}");
        }
    }

    [GeneratedRegex(@"//c(\d)+")]
    private static partial Regex DotPartySubdomainRegex();
    [GeneratedRegex("//c\\d+")]
    private static partial Regex DotPartyReplacementRegex();
}