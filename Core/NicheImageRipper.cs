using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.FileDownloading;
using Core.History;
using Core.Managers;
using Core.Utility;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using File = System.IO.File;

namespace Core;

public partial class NicheImageRipper : IDisposable
{
    private Version? _latestVersion;
    public static Config Config { get; set; } = Config.Instance;
    public static LoggingLevelSwitch ConsoleLoggingLevelSwitch { get; set; } = new();
    public static FlareSolverrManager FlareSolverrManager { get; set; } = new(Config.FlareSolverrUri);
    
    
    public string Title { get; } = "NicheImageRipper";
    public Version Version { get; set; } = new(3, 0, 0, 1);

    public Version LatestVersion => _latestVersion ??= GetLatestVersion().Result;

    public List<string> UrlQueue { get; set; } = [];
    public bool Interrupted { get; set; } = false;
    public ImageRipper? Ripper { get; set; }
    
    public event Action? OnUrlQueueUpdated;
    
    public static bool LiveHistory 
    { 
        get => Config.LiveHistory;
        set => Config.LiveHistory = value;
    }
    
    public static bool AskToReRip
    {
        get => Config.AskToReRip;
        set => Config.AskToReRip = value;
    }

    public static FilenameScheme FilenameScheme
    {
        get => Config.FilenameScheme;
        set => Config.FilenameScheme = value;
    }

    public static UnzipProtocol UnzipProtocol
    {
        get => Config.UnzipProtocol;
        set => Config.UnzipProtocol = value;
    }

    public static PostDownloadAction PostDownloadAction
    {
        get => Config.PostDownloadAction;
        set => Config.PostDownloadAction = value;
    }

    public static string SavePath
    {
        get => Config.SavePath;
        set => Config.SavePath = value;
    }

    public static int MaxRetries
    {
        get => Config.MaxRetries;
        set => Config.MaxRetries = value;
    }

    public static int RetryDelay
    {
        get => Config.RetryDelay;
        set => Config.RetryDelay = value;
    }

    private WebDriverPool WebDriverPool { get; set; } = new(1);
    
    //public List<HistoryEntry> History { get; set; } = [];
    protected static HistoryManager HistoryDb => HistoryManager.Instance;
    
    protected bool Debugging { get; set; }

    public void LoadUrlFile(string filepath)
    {
        var loadedUrls = JsonUtility.Deserialize<List<string>>(filepath)!;
        foreach (var url in loadedUrls)
        {
            AddToUrlQueue(url, noCheck: true);
        }
        OnUrlQueueUpdated?.Invoke();
    }

    private static List<HistoryEntry> LoadHistoryData()
    {
        return HistoryDb.GetHistory();
    }

    public static List<HistoryEntry> GetHistoryPage(int start, int offset)
    {
        return HistoryDb.GetHistory(start, offset);
    }
    
    public static int GetHistoryCount()
    {
        return HistoryDb.GetHistoryEntryCount();
    }

    // FIXME: Error handling is not implemented
    private List<RejectedUrlInfo> QueueUrlsHelper(string urls)
    {
        var urlList = SeparateString(urls, "https://");
        var failedUrls = new List<RejectedUrlInfo>();
        foreach (var (i, url) in urlList.Enumerate())
        {
            var normalizedUrl = NormalizeUrl(url);
            if (normalizedUrl.Contains("http://"))
            {
                var urlsSplit = SeparateString(normalizedUrl, "http://");
                failedUrls.AddRange(urlsSplit.Select(u => AddToUrlQueue(NormalizeUrl(u), i))
                                             .OfType<RejectedUrlInfo>());
            }
            else
            {
                if (UrlUtility.UrlCheck(normalizedUrl))
                {
                    if (!UrlQueue.Any(queuedUrl => CheckIfUrlsAreEqual(queuedUrl, normalizedUrl)))
                    {
                        var result = AddToUrlQueue(normalizedUrl, i);
                        failedUrls.AddIfNotNull(result);
                    }
                    else
                    {
                        failedUrls.Add(new RejectedUrlInfo(normalizedUrl, QueueFailureReason.AlreadyQueued));
                    }
                }
                else
                {
                    failedUrls.Add(new RejectedUrlInfo(normalizedUrl, QueueFailureReason.NotSupported));
                }
            }
        }
        
        return failedUrls;
    }
    
    private static bool CheckIfUrlsAreEqual(string url1, string url2)
    {
        var host1 = new Uri(url1).Host;
        var host2 = new Uri(url2).Host;
        if (host1 != host2)
        {
            return false;
        }
        
        if (host1.Contains("pornhub.com"))
        {
            return PornhubUrlEquality(url1, url2);
        }

        if (host1.Contains("yande") || host1.Contains("danbooru") || host1.Contains("gelbooru") || host1.Contains("rule34") || host1.Contains("booru.com"))
        {
            return BooruUrlEquality(url1, url2);
        }
        
        var normalizedUrl1 = url1.Split("?")[0];
        var normalizedUrl2 = url2.Split("?")[0];
        return normalizedUrl1 == normalizedUrl2;
    }

    private static bool BooruUrlEquality(string url1, string url2)
    {
        var tags1 = BooruRegex().Match(url1).Groups[1].Value;
        var tags2 = BooruRegex().Match(url2).Groups[1].Value;
        return tags1 == tags2;
    }

    private static bool PornhubUrlEquality(string url1, string url2)
    {
        if(url1.Contains("view_video"))
        {
            if(!url2.Contains("view_video"))
            {
                return false;
            }

            var viewKey1 = PornhubViewKeyRegex().Match(url1).Groups[1].Value;
            var viewKey2 = PornhubViewKeyRegex().Match(url2).Groups[1].Value;
            return viewKey1 == viewKey2;
        }

        if (url2.Contains("view_video"))
        {
            return false;
        }

        var url1Parts = url1.Split("/");
        var url2Parts = url2.Split("/");
        return url1Parts[4] == url2Parts[4];
    }
    
    public static string NormalizeUrl(string url)
    {
        var host = new Uri(url).Host;
        
        if (host.Contains("pornhub.com"))
        {
            return NormalizePornhubUrl(url);
        }

        if (host.Contains("yande"))
        {
            return NormalizeBooruUrl(url, Booru.Yandere);
        }
        if (host.Contains("danbooru"))
        {
            return NormalizeBooruUrl(url, Booru.Danbooru);
        }
        if (host.Contains("gelbooru"))
        {
            return NormalizeBooruUrl(url, Booru.Gelbooru);
        }
        if (host.Contains("rule34"))
        {
            return NormalizeBooruUrl(url, Booru.Rule34);
        }

        if (host.Contains("booru.com"))
        {
            // Special case for booru.com as the site is used as a control command and not as a real site
            var tags = BooruRegex().Match(url).Groups[1].Value.Replace("++", "+");
            return $"https://booru.com/post?{tags}";
        }
        
        return url.Split("?")[0];
    }
    
    private static string NormalizePornhubUrl(string url)
    {
        if(url.Contains("view_video"))
        {
            var id = PornhubViewKeyRegex().Match(url).Groups[1].Value;
            return $"https://www.pornhub.com/view_video.php?viewkey={id}";
        }

        var urlParts = url.Split('/');
        return urlParts[..5].Join('/').Split('?')[0];
    }
    
    private static string NormalizeBooruUrl(string url, Booru booru)
    {
        var baseUrl = url.Split("?")[0];
        var tags = BooruRegex().Match(url).Groups[1].Value.Replace("++", "+");
        if (tags.EndsWith('+'))
        {
            tags = tags[..^1];
        }
        
        return booru switch
        {
            Booru.Danbooru => $"{baseUrl}?{tags}",
            Booru.Gelbooru => $"{baseUrl}?page=post&s=list&{tags}",
            Booru.Rule34 => $"{baseUrl}?page=post&s=list&{tags}",
            Booru.Yandere => $"{baseUrl}?{tags}",
            _ => throw new ArgumentOutOfRangeException(nameof(booru), booru, null)
        };
    }

    public async Task Rip()
    {
        Log.Debug("Starting rip");
        while (UrlQueue.Count != 0)
        {
            Log.Debug("Queue size: {QueueCount}", UrlQueue.Count);
            try
            {
                var url = await RipUrl();
                Log.Debug("Ripped URL: \"{Url}\"", url);
                if (url != "")
                {
                    // If empty url is returned Ripper is also null
                    UpdateHistory(Ripper!.FolderInfo, url);
                }
            }
            finally
            {
                Ripper?.Dispose(); // TODO: Fix this
            }
        }
    }

    private async Task<string> RipUrl()
    {
        if (UrlQueue.Count == 0)
        {
            PrintUtility.Print("No URLs to rip.");
            return "";
        }
        
        var url = UrlQueue[0];
        Log.Information("{Url}", url);
        Ripper = new ImageRipper(WebDriverPool, FilenameScheme, UnzipProtocol, PostDownloadAction);
        Log.Debug("Ripper created");
        Interrupted = true;
        var retry = 0;
        do
        {
            try
            {
                await Ripper.Rip(url);
                break;
            }
            catch (Exception e)
            {
                if (retry != MaxRetries - 1)
                {
                    retry++;
                    await Task.Delay(RetryDelay);
                    if (Debugging)
                    {
                        Log.Error(e, "Failed to rip {Url} on attempt {Retry}.", url, retry);
                        LogMessageToFile("Press any key to continue...");
                        Console.ReadKey();
                    }
                    continue;
                }

                Log.Error("Failed to rip {Url} after {MaxRetries} attempts.", url, MaxRetries);
                throw;
            }
        } while (retry < MaxRetries);
        Interrupted = false;
        UrlQueue.RemoveAt(0);
        OnUrlQueueUpdated?.Invoke();
        return url;
    }

    protected void SaveData()
    {
        if (UrlQueue.Count > 0)
        {
            JsonUtility.Serialize("UnfinishedRips.json", UrlQueue);
        }

        // Interrupted is only set after creating ImageRipper
        if (Interrupted && Ripper!.CurrentIndex > 1)
        {
            File.WriteAllText(".ripIndex", Ripper.CurrentIndex.ToString());
        }
        
        Config.Instance.SaveConfig();
    }
    
    public static void ClearCache()
    {
        SilentlyRemoveFiles(".ripIndex", "partial.json");
    }

    private static void SilentlyRemoveFiles(params string[] filepaths)
    {
        foreach (var filepath in filepaths)
        {
            SilentlyRemoveFile(filepath);
        }
    }

    private static void SilentlyRemoveFile(string filepath)
    {
        try
        {
            File.Delete(filepath);
        }
        catch (FileNotFoundException)
        {
            // ignored
        }
    }
    
    /// <summary>
    ///     Split a string while keeping the delimiter attached to each part
    /// </summary>
    /// <param name="baseString">String to split</param>
    /// <param name="delimiter">Delimiter to split the string by</param>
    /// <returns>List of split element from the baseString with delimiters still attached</returns>
    private static IEnumerable<string> SeparateString(string baseString, string delimiter)
    {
        var stringList = baseString.Split(delimiter); // Split by delimiter
        if (stringList[0] == "")
        {
            stringList = stringList[1..];
        }

        return stringList.Select(s => delimiter + s.Trim());
    }
    
    private bool IsLatestVersion()
    {
        return Version >= LatestVersion;
    }
    
    /// <summary>
    ///     Retrieve the latest version of the NicheImageRipper from the remote git repo
    /// </summary>
    /// <returns>Latest version of the NicheImageRipper or 0.0.0 if unable to connect to the repo</returns>
    private static async Task<Version> GetLatestVersion()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
        try
        {
            var response = await client.GetAsync("https://api.github.com/repos/Exiua/NicheImageRipper/releases/latest");
            if(!response.IsSuccessStatusCode)
            {
                return new Version(0, 0, 0);
            }
            
            var jsonString = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<JsonNode>(jsonString);
            if(json is null)
            {
                return new Version(0, 0, 0);
            }
            var versionString = json["tag_name"]?.Deserialize<string>()?.Remove(0, 1);
            if(versionString is null)
            {
                return new Version(0, 0, 0);
            }
            var version = Version.Parse(versionString);
            return version;
        }
        catch (HttpRequestException)
        {
            return new Version(0, 0, 0);
        }
    }
    
    public static void NormalizeUrlsInDb()
    {
        const int batchSize = 1000;
        
        var numEntries = HistoryDb.GetHistoryEntryCount();
        var transaction = HistoryDb.BeginTransaction();
        try
        {
            for (var i = 1; i < numEntries; i++)
            {
                var url = HistoryDb.GetUrlById(i, transaction);
                if (url is null)
                {
                    throw new RipperException("Failed to retrieve URL from database for ID: " + i);
                }
                
                var normalizedUrl = NormalizeUrl(url);
                if (url != normalizedUrl)
                {
                    HistoryDb.UpdateHistoryEntryUrlById(i, normalizedUrl, transaction);
                }

                if (i % batchSize == 0)
                {
                    transaction.Commit();
                    transaction.Dispose();
                    transaction = HistoryDb.BeginTransaction();
                }
            }

            // Commit any remaining entries
            if (numEntries % batchSize != 0)
            {
                transaction.Commit();
            }
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            transaction.Dispose();
        }
    }

    protected virtual RejectedUrlInfo? AddToUrlQueue(string url, int index = -1, bool noCheck = false)
    {
        var normalizedUrl = NormalizeUrl(url);
        if (!noCheck)
        {
            if (HistoryDb.GetHistoryByUrl(normalizedUrl) is not null)
            {
                return new RejectedUrlInfo(normalizedUrl, QueueFailureReason.PreviouslyProcessed, index);
            }
        }
        
        UrlQueue.Add(normalizedUrl);
        return null;
    }
    
    public void QueueUrls(string userInput)
    {
        var startIndex = UrlQueue.Count;
        var offset = 0;
        var failedUrls = QueueUrlsHelper(userInput);
        var updated = failedUrls.Count == 0;
        foreach(var failedUrl in failedUrls)
        {
            switch (failedUrl.Reason)
            {
                case QueueFailureReason.None:
                    break;
                case QueueFailureReason.AlreadyQueued:
                    LogMessageToFile($"URL already queued: {failedUrl.Url}");
                    break;
                case QueueFailureReason.NotSupported:
                    LogMessageToFile($"URL not supported: {failedUrl.Url}");
                    break;
                case QueueFailureReason.PreviouslyProcessed:
                    LogMessageToFile($"Re-rip url (y/n)? {failedUrl.Url}", newLine: false);
                    // TODO: Replace with delegate to defer handling to application on how to get user input
                    var response = Console.ReadLine();
                    if (response == "y")
                    {
                        var correctIndex = startIndex + failedUrl.Index + offset;
                        offset++;
                        UrlQueue.Insert(correctIndex, failedUrl.Url);
                        updated = true;
                    }
                    else
                    {
                        offset--;
                    }
                    break;
                default:
                    throw new InvalidOperationException("Invalid QueueFailureReason: " + failedUrl.Reason);
            }
        }
        
        if (updated)
        {
            OnUrlQueueUpdated?.Invoke();
        }
    }

    public void DequeueUrls(List<string> urlsToRemove)
    {
        var currentCount = UrlQueue.Count;
        UrlQueue = UrlQueue.Except(urlsToRemove).ToList();
        if (currentCount != UrlQueue.Count)
        {
            OnUrlQueueUpdated?.Invoke();
        }
    }

    public virtual void UpdateHistory(RipInfo ripInfo, string url)
    {
        //var duplicate = History.FirstOrDefault(x => x.DirectoryName == ripInfo.DirectoryName);
        var duplicate = HistoryDb.GetHistoryEntryByDirectoryName(ripInfo.DirectoryName);
        if (duplicate is not null)
        {
            var now = DateTime.Now;
            //History.Remove(duplicate);
            duplicate.Date = now;   // Update date
            //History.Add(duplicate); // Move to the end
            HistoryDb.UpdateDateByUrl(url, now);
        }
        else
        {
            var entry = new HistoryEntry(ripInfo.DirectoryName, url, ripInfo.NumUrls);
            //History.Add(entry);
            HistoryDb.InsertHistoryRecord(entry);
        }
    }

    public static void LogMessageToFile(string message, LogEventLevel level = LogEventLevel.Information, bool newLine = true)
    {
        DisableConsoleLogging();
        if (newLine)
        {
            Console.WriteLine(message);
        }
        else
        {
            Console.Write(message);
        }
        Log.Write(level, message);
        EnableConsoleLogging();
    }

    private static void EnableConsoleLogging()
    {
        #if DEBUG
        ConsoleLoggingLevelSwitch.MinimumLevel = LogEventLevel.Debug;
        #else
        ConsoleLoggingLevelSwitch.MinimumLevel = LogEventLevel.Information;
        #endif
    }

    private static void DisableConsoleLogging()
    {
        ConsoleLoggingLevelSwitch.MinimumLevel = LogEventLevel.Fatal + 1;
    }
    
    protected static ExternalFeatureSupport GetExternalFeatureSupport()
    {
        var support = ExternalFeatureSupport.None;
        support |= CheckForFfmpeg() ? ExternalFeatureSupport.Ffmpeg : 0;
        support |= CheckForYtDlp() ? ExternalFeatureSupport.YtDlp : 0;
        support |= CheckForMegaCmd() ? ExternalFeatureSupport.MegaCmd : 0;
        support |= CheckForFlareSolverr() ? ExternalFeatureSupport.FlareSolverr : 0;
        return support;
    }
    
    private static bool CheckForFfmpeg()
    {
        return CheckForProcess("ffmpeg", "-version");
    }
    
    private static bool CheckForYtDlp()
    {
        return CheckForProcess("yt-dlp", "--version");
    }
    
    private static bool CheckForMegaCmd()
    {
        return CheckForProcess("mega-version.bat", "-v");
    }

    private static bool CheckForFlareSolverr()
    {
        return Config.FlareSolverrUri != "";
    }

    private static bool CheckForProcess(string filename, string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = filename,
                Arguments = arguments,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        try
        {
            process.Start();
            process.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        
        return true;
    }
    
    public void Dispose()
    {
        WebDriverPool.Dispose();
        GC.SuppressFinalize(this);
    }

    [GeneratedRegex("viewkey=([0-9a-z]+)")]
    private static partial Regex PornhubViewKeyRegex();
    [GeneratedRegex(@"(tags=[^&]+)")]
    private static partial Regex BooruRegex();
}