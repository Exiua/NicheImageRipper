using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.FileDownloading;
using Core.Utility;
using File = System.IO.File;

namespace Core;

public abstract partial class NicheImageRipper
{
    public static Config Config { get; set; } = Config.Instance;
    
    public string Title { get; set; } = "NicheImageRipper";
    public Version LatestVersion { get; set; } = GetLatestVersion().Result;
    public List<string> UrlQueue { get; set; } = [];
    public bool LiveUpdate { get; set; } = false;
    public bool ReRipAsk { get; set; } = true;
    public bool Interrupted { get; set; } = false;
    public ImageRipper Ripper { get; set; } = null!;
    public FilenameScheme FilenameScheme { get; set; } = FilenameScheme.Original;
    public UnzipProtocol UnzipProtocol { get; set; } = UnzipProtocol.None;
    // self.status_sync: StatusSync = StatusSync()
    // self.ripper_thread: threading.Thread = threading.Thread()
    public Version Version { get; set; } = new(2, 2, 0);
    public string SaveFolder { get; set; } = ".";
    public int MaxRetries { get; set; } = 4;
    public int RetryDelay { get; set; } = 1000; // In milliseconds
    
    public List<HistoryEntry> History { get; set; } = [];
    
    protected bool Debugging { get; set; } = false;

    protected virtual void LoadHistory()
    {
        History = LoadHistoryData();
    }

    protected NicheImageRipper()
    {
        LoadAppData();
    }

    private void LoadAppData()
    {
        if (File.Exists("config.json"))
        {
            LoadConfig();
        }

        if (File.Exists("RipHistory.json"))
        {
            LoadHistory();
        }
    }

    private void LoadConfig()
    {
        var config = Config.Instance;
        SaveFolder = config.SavePath;
        FilenameScheme = config.FilenameScheme;
        UnzipProtocol = config.UnzipProtocol;
        ReRipAsk = config.AskToReRip;
        LiveUpdate = config.LiveHistory;
    }

    protected void LoadUrlFile(string filepath)
    {
        var loadedUrls = JsonUtility.Deserialize<List<string>>(filepath)!;
        foreach (var url in loadedUrls)
        {
            AddToUrlQueue(url);
        }
    }

    private static List<HistoryEntry> LoadHistoryData()
    {
        return JsonUtility.Deserialize<List<HistoryEntry>>("RipHistory.json") ?? [];
    }

    // FIXME: Error handling is not implemented
    protected List<RejectedUrlInfo> QueueUrls(string urls)
    {
        var urlList = SeparateString(urls, "https://");
        var failedUrls = new List<RejectedUrlInfo>();
        foreach (var (i, url) in urlList.Enumerate())
        {
            if (url.Contains("http://"))
            {
                var urlsSplit = SeparateString(url, "http://");
                failedUrls.AddRange(urlsSplit.Select(u => AddToUrlQueue(u, i))
                                             .OfType<RejectedUrlInfo>());
            }
            else
            {
                if (UrlUtility.UrlCheck(url))
                {
                    if (!UrlQueue.Any(queuedUrl => CheckIfUrlsAreEqual(queuedUrl, url)))
                    {
                        var result = AddToUrlQueue(url, i);
                        failedUrls.AddNotNull(result);
                    }
                    else
                    {
                        failedUrls.Add(new RejectedUrlInfo(url, QueueFailureReason.AlreadyQueued));
                    }
                }
                else
                {
                    failedUrls.Add(new RejectedUrlInfo(url, QueueFailureReason.NotSupported));
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

        if (host1.Contains("yande") || host1.Contains("danbooru") || host1.Contains("gelbooru") || host1.Contains("rule34"))
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

    protected async Task<string> RipUrl()
    {
        if (UrlQueue.Count == 0)
        {
            PrintUtility.Print("No URLs to rip.");
            return "";
        }
        var url = UrlQueue[0];
        PrintUtility.Print(url);
        Ripper = new ImageRipper(FilenameScheme, UnzipProtocol);
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
                        Console.WriteLine(e);
                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();
                    }
                    continue;
                }

                PrintUtility.Print($"Failed to rip {url} after {MaxRetries} attempts.");
                throw;
            }
        } while (retry < MaxRetries);
        Interrupted = false;
        UrlQueue.RemoveAt(0);
        return url;
    }

    protected void SaveData()
    {
        if (UrlQueue.Count > 0)
        {
            JsonUtility.Serialize("UnfinishedRips.json", UrlQueue);
        }

        if (Interrupted && Ripper.CurrentIndex > 1)
        {
            File.WriteAllText(".ripIndex", Ripper.CurrentIndex.ToString());
        }
        
        JsonUtility.Serialize("RipHistory.json", History);
        Config.Instance.SaveConfig();
    }
    
    protected static void ClearCache()
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    protected virtual RejectedUrlInfo? AddToUrlQueue(string url, int index = -1)
    {
        if(History.Any(entry => CheckIfUrlsAreEqual(entry.Url, url)))
        {
            return new RejectedUrlInfo(url, QueueFailureReason.PreviouslyProcessed, index);
        }
        
        UrlQueue.Add(url);
        return null;
    }

    public virtual void UpdateHistory(ImageRipper ripper, string url)
    {
        var duplicate = History.FirstOrDefault(x => x.DirectoryName == ripper.FolderInfo.DirectoryName);
        if (duplicate is not null)
        {
            History.Remove(duplicate);
            History.Add(duplicate); // Move to the end
        }
        else
        {
            var folderInfo = ripper.FolderInfo;
            History.Add(new HistoryEntry(folderInfo.DirectoryName, url, folderInfo.NumUrls));
        }
    }

    [GeneratedRegex("viewkey=([0-9a-z]+)")]
    private static partial Regex PornhubViewKeyRegex();
    [GeneratedRegex(@"(tags=[^&]+)")]
    private static partial Regex BooruRegex();
}