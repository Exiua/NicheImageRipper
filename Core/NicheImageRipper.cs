using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using NicheImageRipper.Common.Exceptions;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.History;
using NicheImageRipper.Core.PartialSaves;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.Features;
using NicheImageRipper.Sdk.Managers;
using NicheImageRipper.Sdk.Utility;
using OpenQA.Selenium;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using File = System.IO.File;
using UrlUtility = NicheImageRipper.Core.Utility.UrlUtility;

namespace NicheImageRipper.Core;

public partial class NicheImageRipper : IDisposable
{
    public static string Title => "NicheImageRipper";
    public static GeneralConfig Config => Sdk.Configuration.Config.Instance;
    public static LoggingLevelSwitch ConsoleLoggingLevelSwitch { get; } = new();
    public static FlareSolverrManager FlareSolverrManager => FlareSolverrManager.Instance;
    public static Version Version { get; } = new(5, 0, 0);

    public static IAvailableFeatures AvailableFeatures { get; } = AvailableFeatureManager.AvailableFeatures;

    protected ILogger Logger { get; } = Log.ForContext<NicheImageRipper>();

    public Version LatestVersion => field ??= GetLatestVersion().Result;

    public List<string> UrlQueue { get; set; } = [];
    public bool Interrupted { get; set; }
    public ImageRipper? Ripper { get; set; }
    public bool Paused => Ripper?.Paused ?? false;

    public event Action? OnUrlQueueUpdated;
    public event Action<int, int>? OnProgressChanged;

    // TODO: Convert to enum with better variations on how to handle re-ripping
    public static bool AskToReRip
    {
        get => Config.AskToReRip;
        set => Config.AskToReRip = value;
    }

    public static bool SkipFailedDownloads
    {
        get => Config.SkipFailedDownloads;
        set => Config.SkipFailedDownloads = value;
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

    public static bool SaveUnfinishedUrls
    {
        get => Config.SaveUnfinishedUrls;
        set => Config.SaveUnfinishedUrls = value;
    }

    private WebDriverPool WebDriverPool { get; } = new(1);

    protected static HistoryManager HistoryDb => HistoryManager.Instance;

    protected bool Debugging { get; set; }

    private CancellationTokenSource _cancellationTokenSource = new();
    private Exception? _lastException;
    private bool _disposed;

    static NicheImageRipper()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
    }

    public void LoadUrls(List<string> loadedUrls)
    {
        foreach (var url in loadedUrls)
        {
            AddToUrlQueue(url, noCheck: true);
        }

        OnUrlQueueUpdated?.Invoke();
    }
    
    /// <summary>
    ///     Loads URLs from a specific unfinished-URLs snapshot file (e.g. one the user picked in the
    ///     GUI's file browser) and binds future saves to that same file, so it gets updated in place as
    ///     URLs finish and deleted once the queue empties, rather than the normal subset-scan/new-file
    ///     logic picking a different target.
    /// </summary>
    public void LoadUrlsFromFile(string path)
    {
        var loadedUrls = JsonUtility.Deserialize<List<string>>(path)!;
        LoadUrls(loadedUrls);
        _unfinishedUrlsPath = path;
    }

    public void QueueUrlsFromEmptyDirectories()
    {
        var directories = Directory.GetDirectories(SavePath, "*", SearchOption.TopDirectoryOnly);
        foreach (var dir in directories)
        {
            if (Directory.GetFiles(dir).Length != 0)
            {
                continue;
            }

            var dirName = new DirectoryInfo(dir).Name;
            var historyEntry = HistoryDb.GetHistoryEntryByDirectoryName(dirName);
            if (historyEntry is not null)
            {
                AddToUrlQueue(historyEntry.Url, noCheck: true);
            }
        }
    }

    public bool Resume()
    {
        if (Ripper is null)
        {
            return false;
        }

        Ripper.Paused = false;
        return true;
    }

    public bool Pause()
    {
        if (Ripper is null)
        {
            return false;
        }

        Ripper.Paused = true;
        return true;
    }

    public static List<HistoryEntry> GetHistoryPage(int start, int offset, HistoryFilter? filter = null)
    {
        return HistoryDb.GetHistory(start, offset, filter);
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
                    if (UrlQueue.All(queuedUrl => queuedUrl != normalizedUrl))
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
    
    public static string NormalizeUrl(string url)
    {
        var host = new Uri(url).Host;

        foreach (var (pattern, kind, normalize) in HtmlParserFactory.UrlNormalizers)
        {
            var matches = kind switch
            {
                UrlMatchKind.StartsWith => host.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
                UrlMatchKind.Contains => host.Contains(pattern, StringComparison.OrdinalIgnoreCase),
                UrlMatchKind.EndsWith => host.EndsWith(pattern, StringComparison.OrdinalIgnoreCase),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };

            if (matches)
            {
                return normalize(url);
            }
        }

        return url.Split("?")[0];
    }

    public async Task<bool> Rip(CancellationToken cancellationToken = default)
    {
        Ripper ??= new ImageRipper(WebDriverPool, FilenameScheme, UnzipProtocol, PostDownloadAction);
        Ripper.OnProgressChanged += OnProgressChangedHandler;
        Logger.Debug("Ripper created");
        Logger.Debug("Starting rip");
        while (UrlQueue.Count != 0)
        {
            Logger.Debug("Queue size: {QueueCount}", UrlQueue.Count);
            var url = await RipUrl(cancellationToken);
            if (url is null)
            {
                // Indicates out of disk space // TODO: Improve return value to be more meaningful
                return false;
            }

            Logger.Debug("Ripped URL: {Url:l}", url);
            if (url != "")
            {
                // If empty url is returned Ripper is also null
                UpdateHistory(Ripper!.FolderInfo, url);
            }

            SaveUnfinishedUrlsToDisk(); // Save after each rip to avoid data loss
        }

        return true;
    }

    private async Task<string?> RipUrl(CancellationToken cancellationToken = default)
    {
        if (UrlQueue.Count == 0)
        {
            Logger.Information("No URLs to rip.");
            return "";
        }

        var url = UrlQueue[0];
        Logger.Information("{Url:l}", url);
        Interrupted = true;

        for (var retry = 0; retry < MaxRetries; retry++)
        {
            try
            {
                var start = DateTime.Now;
                await Ripper!.Rip(url, cancellationToken);
                var elapsed = DateTime.Now - start;
                var elapsedFormatted =
                    $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds:D3}";
                Logger.Information("Ripped {Url:l} in {Elapsed:l}", url, elapsedFormatted);
                //OnUrlRipComplete?.Invoke();
                _lastException = null;
                break;
            }
            catch (WebDriverException e) when (e.Message.Contains("The HTTP request to the remote WebDriver",
                                                   "timed out") && retry < MaxRetries - 1)
            {
                Logger.Error("Failed to rip {Url} due to WebDriver timeout. Retrying... ({Retry}/{MaxRetries})", url,
                    retry + 1, MaxRetries);
                await Task.Delay(10000, cancellationToken);
            }
            catch (NotEnoughDiskSpaceException e)
            {
                _lastException = e;
                Logger.Error("Failed to rip {Url} due to not enough disk space. Terminating...", url);
                return null;
            }
            catch (Exception e)
            {
                if (retry == MaxRetries - 1)
                {
                    _lastException = e;
                    Logger.Error("Failed to rip {Url} after {MaxRetries} attempts.", url, MaxRetries);
                    throw;
                }

                await Task.Delay(RetryDelay, cancellationToken);
                if (Debugging)
                {
                    Logger.Error(e, "Failed to rip {Url} on attempt {Retry}.", url, retry);
                    LogMessageToFile("Press any key to continue...");
                    Console.ReadKey();
                }
            }
        }

        Interrupted = false;
        UrlQueue.RemoveAt(0);
        OnUrlQueueUpdated?.Invoke();
        return url;
    }

    private void OnProgressChangedHandler(int current, int total)
    {
        OnProgressChanged?.Invoke(current, total);
    }

    public async Task SaveData(CancellationToken cancellationToken = default)
    {
        SaveUnfinishedUrlsToDisk();

        // Interrupted is only set after creating ImageRipper
        if (Interrupted && Ripper!.CurrentIndex > 1)
        {
            await Ripper.SaveCurrentRipPosition(cancellationToken);
        }

        Config.SaveConfig();
    }

    private const string UnfinishedUrlsDirectory = "UnfinishedRips";
    private const string LegacyUnfinishedUrlsFile = "UnfinishedRips.json";

    private string? _unfinishedUrlsPath;

    private void SaveUnfinishedUrlsToDisk()
    {
        if (!SaveUnfinishedUrls)
        {
            return;
        }

        if (UrlQueue.Count == 0)
        {
            if (_unfinishedUrlsPath is not null)
            {
                SilentlyRemoveFile(_unfinishedUrlsPath);
                _unfinishedUrlsPath = null;
            }

            return;
        }

        _unfinishedUrlsPath ??= ResolveUnfinishedUrlsPath();
        JsonUtility.Serialize(_unfinishedUrlsPath, UrlQueue);
    }

    /// <summary>
    ///     Picks which unfinished-URLs snapshot file this run should write to. If an existing saved
    ///     list's contents are a superset of the current queue (i.e., the current queue looks like the
    ///     remainder of that saved run), that file is reused and updated in place as URLs finish. If no
    ///     existing saved list matches, a new file is created, so an unrelated batch of URLs doesn't
    ///     overwrite previously-saved unfinished work from a different session.
    /// </summary>
    private string ResolveUnfinishedUrlsPath()
    {
        Directory.CreateDirectory(UnfinishedUrlsDirectory);
        MigrateLegacyUnfinishedUrlsFile();

        var currentUrls = UrlQueue.ToHashSet();
        foreach (var candidatePath in Directory.EnumerateFiles(UnfinishedUrlsDirectory, "*.json"))
        {
            List<string>? savedUrls;
            try
            {
                savedUrls = JsonUtility.Deserialize<List<string>>(candidatePath);
            }
            catch (JsonException)
            {
                continue; // Ignore unreadable/corrupt snapshot files
            }

            if (savedUrls is not null && currentUrls.IsSubsetOf(savedUrls))
            {
                Logger.Debug("Resuming unfinished URL list {Path}", candidatePath);
                return candidatePath;
            }
        }

        var newPath = Path.Combine(UnfinishedUrlsDirectory, $"{Guid.NewGuid():N}.json");
        Logger.Debug("Starting new unfinished URL list {Path}", newPath);
        return newPath;
    }

    private static void MigrateLegacyUnfinishedUrlsFile()
    {
        if (!File.Exists(LegacyUnfinishedUrlsFile))
        {
            return;
        }

        try
        {
            var destination = Path.Combine(UnfinishedUrlsDirectory, $"{Guid.NewGuid():N}.json");
            File.Move(LegacyUnfinishedUrlsFile, destination);
        }
        catch (IOException)
        {
            // Best-effort; leave the legacy file in place if it's locked or otherwise unmovable.
        }
    }

    public static Task SkipEntry(CancellationToken cancellationToken = default)
    {
        return ImageRipper.IncrementCurrentRipPosition(cancellationToken);
    }

    public static void ClearCache()
    {
        PartialSaveManager.Instance.ClearPartialSaves();
        SilentlyRemoveFiles(".ripIndex", "ripState.json");
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
    private static async Task<Version> GetLatestVersion(CancellationToken cancellationToken = default)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
        try
        {
            var response = await client.GetAsync("https://api.github.com/repos/Exiua/NicheImageRipper/releases/latest",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new Version(0, 0, 0);
            }

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonSerializer.Deserialize<JsonNode>(jsonString);
            if (json is null)
            {
                return new Version(0, 0, 0);
            }

            var versionString = json["tag_name"]?.Deserialize<string>()?.Remove(0, 1);
            if (versionString is null)
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

    protected static void NormalizeUrlsInDb()
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
            var searchUrl = normalizedUrl.Replace("e-hentai.org", "exhentai.org");
            if (HistoryDb.GetHistoryByUrl(searchUrl) is not null)
            {
                return new RejectedUrlInfo(normalizedUrl, QueueFailureReason.PreviouslyProcessed, index);
            }
        }

        UrlQueue.Add(normalizedUrl);
        return null;
    }

    public RejectedUrlsInfo QueueUrls(string userInput)
    {
        var currentCount = UrlQueue.Count;
        var rejectedUrls = new RejectedUrlsInfo(currentCount);
        var failedUrls = QueueUrlsHelper(userInput);
        if (currentCount != UrlQueue.Count)
        {
            OnUrlQueueUpdated?.Invoke();
        }

        return rejectedUrls.WithRejectedUrls(failedUrls);
    }

    public void ForceQueueUrls(string url)
    {
        var normalizedUrl = NormalizeUrl(url);
        if (UrlQueue.All(queuedUrl => queuedUrl != normalizedUrl))
        {
            UrlQueue.Add(normalizedUrl);
            OnUrlQueueUpdated?.Invoke();
        }
        else
        {
            Logger.Warning("URL {Url} is already in the queue.", normalizedUrl);
        }
    }

    public void RequeueUrls(RejectedUrlsInfo rejectedUrlsInfo)
    {
        var urls = rejectedUrlsInfo.Urls;
        var startIndex = rejectedUrlsInfo.StartIndex;
        if (urls.Count == 0)
        {
            return;
        }

        Logger.Debug("Re-queuing {Count} URLs", urls.Count);
        var offset = 0;
        foreach (var url in urls)
        {
            UrlQueue.Insert(startIndex + offset, url.Url);
            offset++;
        }

        OnUrlQueueUpdated?.Invoke();
    }

    public void DequeueUrls(IEnumerable<string> urlsToRemove)
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
        var duplicate = HistoryDb.GetHistoryEntryByDirectoryName(ripInfo.DirectoryName);
        if (duplicate is not null)
        {
            Logger.Debug("Duplicate found: {Url}; Updating...", url);
            HistoryDb.UpdateDateByUrl(url, DateTime.Now);
        }
        else
        {
            Logger.Debug("Adding to history: {Url}", url);
            var entry = new HistoryEntry(ripInfo.DirectoryName, url, ripInfo.NumUrls);
            HistoryDb.InsertHistoryRecord(entry);
        }
    }

    // TODO: This should probably be replaced with a proper TUI sink
    public static void LogMessageToFile(string message, LogEventLevel level = LogEventLevel.Information,
                                        bool newLine = true)
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Ripper is not null)
        {
            Ripper.OnProgressChanged -= OnProgressChangedHandler;
            Ripper.Dispose();
        }

        WebDriverPool.Dispose();
        GC.SuppressFinalize(this);
    }

    ~NicheImageRipper()
    {
        Dispose();
    }

    [GeneratedRegex("viewkey=([0-9a-z]+)")]
    private static partial Regex PornhubViewKeyRegex();

    [GeneratedRegex(@"(tags=[^&]+)")]
    private static partial Regex BooruRegex();
}