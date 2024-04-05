using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Configuration;
using Core.Enums;
using Core.FileDownloading;
using Core.Utility;
using File = System.IO.File;

namespace Core;

public abstract class NicheImageRipper
{
    public static Config Config { get; set; } = Config.Instance;
    
    public string Title { get; set; } = "NicheImageRipper";
    public Version LatestVersion { get; set; } = GetLatestVersion().Result;
    public Queue<string> UrlQueue { get; set; } = new();
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
    
    public List<List<string>> History { get; set; } = [];

    protected abstract List<List<string>> GetHistoryData();
    protected abstract void LoadHistory();
    public abstract void UpdateHistory(ImageRipper ripper, string url);

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
        var loadedUrls = JsonUtility.Deserialize<List<string>>(filepath);
        foreach (var url in loadedUrls)
        {
            AddToUrlQueue(url);
        }
    }
    
    protected abstract void AddToUrlQueue(string url);

    protected static List<List<string>> LoadHistoryData()
    {
        return JsonUtility.Deserialize<List<List<string>>>("RipHistory.json");
    }

    protected void QueueUrls(string urls)
    {
        var urlList = SeparateString(urls, "https://");
        foreach (var url in urlList)
        {
            if (url.Contains("http://"))
            {
                var urlsSplit = SeparateString(url, "http://");
                foreach (var u in urlsSplit)
                {
                    AddToUrlQueue(u);
                }
            }
            else
            {
                if (UrlUtility.UrlCheck(url))
                {
                    if (!UrlQueue.Contains(url))
                    {
                        AddToUrlQueue(url);
                    }
                    else
                    {
                        // if result < QueueResult.ALREADY_QUEUED:
                        //     result = QueueResult.ALREADY_QUEUED
                    }
                }
                else
                {
                    // if result < QueueResult.NOT_SUPPORTED:
                    //     result = QueueResult.NOT_SUPPORTED
                }
            }
        }
    }

    protected async Task<string> RipUrl()
    {
        if (UrlQueue.Count == 0)
        {
            Console.WriteLine("No URLs to rip.");
            return "";
        }
        var url = UrlQueue.Peek();
        Console.WriteLine(url);
        Ripper = new ImageRipper(FilenameScheme, UnzipProtocol);
        Interrupted = true;
        await Ripper.Rip(url);
        Interrupted = false;
        UrlQueue.Dequeue();
        return url;
    }
    
    public void SaveData()
    {
        if (UrlQueue.Count > 0)
        {
            JsonUtility.Serialize("UnfinishedRips.json", UrlQueue);
        }

        if (Interrupted && Ripper.CurrentIndex > 1)
        {
            File.WriteAllText(".ripIndex", Ripper.CurrentIndex.ToString());
        }
        
        JsonUtility.Serialize("RipHistory.json", GetHistoryData());
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
}