using System.Collections.Concurrent;
using System.Diagnostics;
using NicheImageRipper.Core.GalleryUpdates;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.PartialSaves;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Utility;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.FileDownloading;
using NicheImageRipper.Sdk.SiteParsing;

using NicheImageRipper.Sdk.Utility;
using Serilog;
using UrlUtility = NicheImageRipper.Sdk.Utility.UrlUtility;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.Core.FileDownloading;

/// <summary>
///     Orchestrates a single site rip: parses the target URL via the appropriate <see cref="HtmlParserOrchestrator"/>,
///     resolves the resulting <see cref="RipInfo"/> into a concrete download plan (list, manual generation,
///     or external-tool delegation), executes that plan through the registered <see cref="IFileDownloadStrategy"/>
///     implementations, and persists/restores progress across interrupted runs.
/// </summary>
public class FileRipper : IDisposable
{
    internal const string RipIndexPath = ".ripIndex";

    private const string RipStatePath = "ripState.json";
    private const int MillisecondsInSecond = 1000;

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

    /// <summary>
    /// The parsed rip plan for the current/most recent <see cref="Rip"/> call — site name, download mode, and
    /// the resolved file list or manual-generation parameters.
    /// </summary>
    public RipInfo FolderInfo { get; private set; } = null!;

    /// <summary>
    /// Whether the most recent <see cref="Rip"/> was an update of a tracked gallery that had nothing new to download.
    /// </summary>
    public bool NoNewItems { get; private set; }

    /// <summary>
    ///     URLs the most recent <see cref="Rip"/> call couldn't parse or download, one entry per URL. A file that
    ///     fails and then succeeds on a later attempt is dropped, so this is only what was still failing at the end.
    ///     Not written to <see cref="FailedUrlLog"/> here: the caller does that once it stops retrying the gallery.
    /// </summary>
    public IReadOnlyCollection<FailedUrlEntry> Failures => _failures.Values.ToList();

    private readonly ConcurrentDictionary<string, FailedUrlEntry> _failures = new();

    /// <summary>
    /// Post-download behaviors to apply per file (currently: duplicate removal).
    /// </summary>
    public PostDownloadAction PostDownloadAction { get; set; }

    private string GivenUrl { get; set; }
    private Dictionary<string, bool> PersistentLogins { get; }
    private string SavePath { get; }
    private HttpClient Session { get; }
    private string SiteName { get; set; }
    private float SleepTime { get; set; }

    /// <summary>
    /// Index of the file currently being downloaded, for progress display/resume purposes.
    /// </summary>
    public int CurrentIndex { get; private set; }

    private double FailureThreshold { get; set; } = 0.5;
    private WebDriverPool DriverPool { get; }
    private WebDriver WebDriver { get; set; }
    private ILogger Logger { get; }

    /// <summary>
    /// When set, download loops poll this before each item/attempt and wait until it clears.
    /// </summary>
    public bool Paused { get; set; }

    private FileDownloadStrategyRegistry StrategyRegistry { get; }
    private IReadOnlyList<IRequestHeaderModifier> HeaderModifiers { get; }
    private IReadOnlyList<IDownloadErrorHandler> ErrorHandlers { get; }
    private IReadOnlyList<IDownloadExceptionHandler> ExceptionHandlers { get; }
    private IReadOnlyList<IPostDownloadValidator> PostDownloadValidators { get; }
    private IReadOnlyList<IExternalToolDownloadStrategy> ExternalToolStrategies { get; }
    private IReadOnlyList<IWebDriverPreferenceProvider> WebDriverPreferenceProviders { get; }
    private DownloadContext DownloadContext { get; set; } = null!;
    private readonly GenericHttpDownloadStrategy _genericHttpStrategy = new();

    // Indices into FolderInfo.Urls that finished downloading this rip (or before the point it resumed from), so a
    // gallery update only records items that actually made it to disk
    private readonly HashSet<int> _finishedDownloads = [];
    private readonly Lock _finishedDownloadsLock = new();

    /// <summary>
    /// Whether exceeding <see cref="FailureThreshold"/> should also clear this URL's cached
    /// <see cref="PartialSaveManager"/> entry, since the cached links may no longer be valid.
    /// </summary>
    private bool ClearPartialSaveOnFailureThreshold { get; }

    private bool _disposed;

    private static GeneralConfig Config => Sdk.Configuration.Config.Instance;

    /// <summary>
    /// Raised with (current, total) progress after each file/step; (0,0) signals indeterminate progress.
    /// </summary>
    public delegate void ProgressChangedHandler(int current, int total);

    /// <inheritdoc cref="ProgressChangedHandler"/>
    public event ProgressChangedHandler? OnProgressChanged;

    /// <summary>
    ///     Constructs a ripper bound to a driver pool and a fixed set of rip-wide options. A fresh
    ///     <see cref="WebDriver"/> is acquired immediately and held until <see cref="Dispose"/>.
    /// </summary>
    /// <param name="driverPool">Pool to acquire/release Selenium drivers from for the lifetime of this instance.</param>
    /// <param name="filenameScheme">How filenames are generated for downloaded files.</param>
    /// <param name="unzipProtocol">Whether/how to extract archives found in the output directory after a successful rip.</param>
    /// <param name="postDownloadAction">Per-file behaviors to apply after each successful download.</param>
    /// <param name="clearPartialSaveOnFailureThreshold">Whether to clear this URL's cached partial save when the failure threshold is exceeded, since the cached links may be stale.</param>
    public FileRipper(WebDriverPool driverPool, FilenameScheme filenameScheme = FilenameScheme.Original,
                       UnzipProtocol unzipProtocol = UnzipProtocol.None,
                       PostDownloadAction? postDownloadAction = null,
                       bool clearPartialSaveOnFailureThreshold = true)
    {
        FilenameScheme = filenameScheme;
        UnzipProtocol = unzipProtocol;
        PostDownloadAction = postDownloadAction ?? new PostDownloadAction();
        ClearPartialSaveOnFailureThreshold = clearPartialSaveOnFailureThreshold;
        GivenUrl = "";
        PersistentLogins = new Dictionary<string, bool>();
        // Create copy of save path, so that config can be changed without affecting current ripper instance
        SavePath = Config.SavePath;
        Session = new HttpClient();
        SiteName = "";
        SleepTime = 0.2f;
        CurrentIndex = 0;
        DriverPool = driverPool;
        WebDriver = driverPool.AcquireDriver(true);
        Logger = Log.ForContext<FileRipper>();

        StrategyRegistry = DownloadCompositionRoot.BuildStrategyRegistry();
        HeaderModifiers = DownloadCompositionRoot.BuildHeaderModifiers();
        ErrorHandlers = DownloadCompositionRoot.BuildErrorHandlers();
        ExceptionHandlers = DownloadCompositionRoot.BuildExceptionHandlers();
        PostDownloadValidators = DownloadCompositionRoot.BuildPostDownloadValidators();
        ExternalToolStrategies = DownloadCompositionRoot.BuildExternalToolStrategies();
        WebDriverPreferenceProviders = DownloadCompositionRoot.BuildWebDriverPreferenceProviders();
    }

    /// <summary>
    ///     Rips the given URL: determines the site, parses it into a <see cref="RipInfo"/>, and downloads
    ///     everything described by that plan. Safe to call multiple times on the same instance; not safe
    ///     to call concurrently with itself.
    /// </summary>
    /// <param name="url">The page/gallery URL to rip.</param>
    /// <param name="cancellationToken">Cancels the in-progress rip; partial progress is saved where possible.</param>
    /// <exception cref="ObjectDisposedException">This instance has already been disposed.</exception>
    public async Task Rip(string url, CancellationToken cancellationToken = default)
    {
        // Cannot rip if the instance is disposed, but everything else is fine to access
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FileRipper), "Cannot use a disposed ImageRipper instance");
        }

        OnProgressChanged?.Invoke(0, 0); // Indeterminate progress at the start

        _failures.Clear();
        GivenUrl = UrlUtility.NormalizeUrl(url);
        (SiteName, SleepTime) = Core.Utility.UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        Logger.Debug("Site Name: {SiteName}", SiteName);

        await FileGetter(cancellationToken);
    }

    /// <summary>
    ///     Ensures <see cref="WebDriver"/> matches what the current site requires (headless vs. non-headless),
    ///     swapping it out via <see cref="DriverPool"/> only when the current driver doesn't match.
    /// </summary>
    private void LoadCorrectWebDriver()
    {
        var needsNonHeadless = WebDriverPreferenceProviders.Any(p => p.AppliesTo(SiteName) && p.RequiresNonHeadless);

        if (needsNonHeadless == !WebDriver.IsHeadless)
        {
            return; // already on the correct driver
        }

        var previous = WebDriver;
        Logger.Debug("Releasing {DriverType} driver", previous.IsHeadless ? "headless" : "non-headless");
        DriverPool.ReleaseDriver(previous);
        Logger.Debug("Acquiring {DriverType} driver", needsNonHeadless ? "non-headless" : "headless");
        try
        {
            WebDriver = DriverPool.AcquireDriver(!needsNonHeadless);
        }
        catch
        {
            // Take back a driver of the previous kind so WebDriver always refers to one this ripper holds;
            // otherwise Dispose() would release the already-released driver a second time
            WebDriver = DriverPool.AcquireDriver(previous.IsHeadless);
            throw;
        }
    }

    /// <summary>
    ///     Determines which index to resume from: a saved <see cref="RipIndexPath"/> for this exact URL if one
    ///     exists, otherwise the mode-appropriate default (1 for manual generation, 0 otherwise).
    /// </summary>
    private async Task<int> GetStartIndex(CancellationToken cancellationToken = default)
    {
        var defaultStart = FolderInfo.DownloadMode == DownloadMode.Generate ? 1 : 0;

        if (!File.Exists(RipIndexPath))
        {
            return defaultStart;
        }

        var (saveUrl, start) = await CacheUtility.ReadRipIndex(RipIndexPath, cancellationToken);
        if (saveUrl != GivenUrl)
        {
            return defaultStart;
        }

        Logger.Information("Resuming from index {StartIndex}", start);
        File.Delete(RipIndexPath);
        return start;
    }

    /// <summary>
    /// Builds the per-rip context passed to every <see cref="IFileDownloadStrategy"/> and related extension point.
    /// </summary>
    private DownloadContext BuildDownloadContext() => new()
    {
        RequestHeaders = RequestHeaders,
        WebDriver = WebDriver,
        Session = Session,
        SiteName = SiteName,
        SleepTime = SleepTime,
        Logger = Logger,
        HeaderModifiers = HeaderModifiers,
        ErrorHandlers = ErrorHandlers,
        ExceptionHandlers = ExceptionHandlers,
        PostDownloadValidators = PostDownloadValidators,
    };

    /// <summary>Adds to <see cref="Failures"/>, replacing any earlier failure of the same URL.</summary>
    private void RecordFailure(FailureStage stage, string source, string url, FailureKind kind, string? reason)
    {
        var galleryUrl = url == GivenUrl ? null : GivenUrl;
        _failures[url] = FailedUrlEntry.Create(stage, kind, source, url, galleryUrl, reason);
    }

    /// <summary>
    ///     Records a failed (or a skipped-but-missing) download in <see cref="Failures"/>, or clears an earlier failure
    ///     of the URL once it downloads.
    /// </summary>
    private void RecordDownloadResult(IFileDownloadStrategy strategy, string url, DownloadResult result)
    {
        if (result.Outcome == DownloadOutcome.Failed ||
            result is { Outcome: DownloadOutcome.SkipNotAFailure, FailureKind: not null })
        {
            RecordFailure(FailureStage.Download, strategy.GetType().Name, url, result.FailureKind ?? FailureKind.Other,
                result.Reason);
        }
        else
        {
            _failures.TryRemove(url, out _);
        }
    }

    /// <summary>
    ///     Whether an exception means the URL couldn't be handled, as opposed to the rip being stopped (cancellation,
    ///     full disk) or an exception used for control flow (expired links, extension guessing).
    /// </summary>
    private static bool IsFailureToRecord(Exception e) =>
        e is not (OperationCanceledException or UrlExpiredException or WrongExtensionException) &&
        !DiskSpaceUtility.IsDiskFull(e);

    /// <summary>
    ///     Top-level rip pipeline: selects the driver, parses the site, loads any resumable state, dispatches
    ///     to the download mode described by <see cref="FolderInfo"/>, and finalizes the rip (archive
    ///     extraction, failure-threshold check, stats logging) whether it succeeded.
    /// </summary>
    private async Task FileGetter(CancellationToken cancellationToken = default)
    {
        LoadCorrectWebDriver();
        DownloadContext = BuildDownloadContext();

        //var htmlParser = HtmlParserOrchestrator.GetParser(SiteName, );
        //Logger.Debug("Constructed HtmlParser");
        var htmlParser = new HtmlParserOrchestrator(WebDriver, RequestHeaders, FilenameScheme);
        try
        {
            FolderInfo = await htmlParser.ParseSite(GivenUrl, cancellationToken);
        }
        catch (Exception e) when (IsFailureToRecord(e))
        {
            RecordFailure(FailureStage.Parse, htmlParser.ParserTypeName ?? nameof(HtmlParserOrchestrator), GivenUrl,
                FailedUrlLog.Classify(e), e.Message);
            throw;
        }

        var updatePlan = htmlParser.UpdatePlan;
        NoNewItems = false;
        var fullPath = Path.Combine(SavePath, FolderInfo.DirectoryName);
        Logger.Debug("Dir Length: {DirLength}", fullPath.Length);
        Directory.CreateDirectory(fullPath);

        if (updatePlan is not null && PrepareGalleryUpdate(updatePlan, fullPath))
        {
            return;
        }

        var start = await GetStartIndex(cancellationToken);
        lock (_finishedDownloadsLock)
        {
            _finishedDownloads.Clear();
            if (FolderInfo.DownloadMode == DownloadMode.List)
            {
                _finishedDownloads.UnionWith(Enumerable.Range(0, start));
            }
        }

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
        if (updatePlan is not null)
        {
            CompleteGalleryUpdate(updatePlan, fullPath);
        }
    }

    /// <summary>
    ///     Moves existing files to their new positions (once per plan, even if the rip is resumed) and, when there's
    ///     nothing to download, finishes the update straight away.
    /// </summary>
    /// <returns>Whether the update is already complete.</returns>
    private bool PrepareGalleryUpdate(GalleryUpdatePlan plan, string fullPath)
    {
        if (!plan.RenamesApplied)
        {
            GalleryFileRenamer.Apply(fullPath, plan.Renames);
            GalleryStateManager.Instance.SaveRenamedItems(GivenUrl, plan);
        }

        if (!plan.HasNothingToDownload)
        {
            return false;
        }

        GalleryStateManager.Instance.CompleteUpdate(GivenUrl, plan.Cursor, []);
        PartialSaveManager.Instance.RemovePartialSave(GivenUrl);
        NoNewItems = true;
        Logger.Information("No new items for {Url}", GivenUrl);
        OnProgressChanged?.Invoke(1, 1);
        return true;
    }

    /// <summary>
    ///     Records the items that downloaded (failed ones are retried by the next update), removes files that
    ///     re-downloaded items replaced, and drops the partial save so the next rip parses the gallery again.
    /// </summary>
    private void CompleteGalleryUpdate(GalleryUpdatePlan plan, string fullPath)
    {
        List<PendingGalleryItem> finished;
        lock (_finishedDownloadsLock)
        {
            finished = plan.PendingItems.Where(p => _finishedDownloads.Contains(p.DownloadIndex)).ToList();
        }

        var downloaded = finished
                         .Select(p => new GalleryItem(p.Key, p.Version, p.Position, FolderInfo.Urls[p.DownloadIndex].Filename))
                         .ToList();
        foreach (var item in finished)
        {
            // Only remove the old file if no other item ended up with that name
            if (item.ReplacedFilename is { } replaced &&
                !plan.ExistingItems.Any(i => i.Key != item.Key && i.Filename == replaced) &&
                !downloaded.Any(i => i.Filename == replaced))
            {
                var path = Path.Combine(fullPath, replaced);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
        }

        GalleryStateManager.Instance.CompleteUpdate(GivenUrl, plan.Cursor, downloaded);
        PartialSaveManager.Instance.RemovePartialSave(GivenUrl);
        var notFinished = plan.PendingItems.Count - downloaded.Count;
        if (notFinished > 0)
        {
            Logger.Information("{Count} items didn't download and will be retried by the next update", notFinished);
        }
    }

    /// <summary>
    /// Loads persisted <see cref="DownloadStats"/>/<see cref="IndexedHashes"/> from a prior interrupted rip, or creates fresh ones if none exist.
    /// </summary>
    /// <exception cref="RipperException">A rip-state file exists but failed to deserialize.</exception>
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

    /// <summary>Persists the current <see cref="DownloadStats"/>/<see cref="IndexedHashes"/> to disk so an interrupted rip can resume.</summary>
    /// <param name="downloadStats">The current download statistics.</param>
    /// <param name="filesHashes">The current indexed file hashes.</param>
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

    /// <summary>
    ///     Runs after the download-mode dispatch completes without throwing: clears resumable state, checks the
    ///     failure threshold (clearing the cached partial save if configured to), extracts archives if requested,
    ///     writes out a failed-downloads log, and reports final stats.
    /// </summary>
    /// <exception cref="RipperException">More than <see cref="FailureThreshold"/> of files failed to download.</exception>
    private async Task FinalizeRip(string fullPath, DownloadStats downloadStats,
                                   CancellationToken cancellationToken = default)
    {
        File.Delete(RipIndexPath);
        File.Delete(RipStatePath); // Existence of rip state file indicates incomplete rip

        await PostProcessDirectoryLevel(fullPath, cancellationToken);

        if (((double)downloadStats.FailedDownloadsCount) / FolderInfo.NumUrls > FailureThreshold)
        {
            var msg = $"More than {FailureThreshold * 100}% of the images failed to download";
            var e = new RipperException(msg);
            Logger.Error(e, "{msg:l}", msg);
            if (ClearPartialSaveOnFailureThreshold)
            {
                PartialSaveManager.Instance.RemovePartialSave(GivenUrl);
            }

            throw e;
        }

        if (UnzipProtocol != UnzipProtocol.None)
        {
            var (extracted, failed) = new ArchiveExtractor(UnzipProtocol, Logger).ExtractAll(fullPath);
            downloadStats.ArchivesExtracted += extracted;
            downloadStats.ArchivesExtractionFailed += failed;
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

    /// <summary>
    ///     Downloads a numerically-named image set by incrementing a file index against a fixed base URL
    ///     (e.g. https://domain/gallery/01.jpg, 02.jpg, ...), trying each candidate extension in turn until
    ///     one succeeds. Used for sites where no per-file URL list is available up front.
    /// </summary>
    private async Task HandleGeneratingManually(int start, string fullPath, IndexedHashes filesHashes,
                                                DownloadStats downloadStats,
                                                CancellationToken cancellationToken = default)
    {
        // Gets the general url for all images in this album
        var imageLink = FolderInfo.Urls[0];
        var trimmedUrl = Core.Utility.UrlUtility.TrimUrl(imageLink.Url);
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
                    var result = await DownloadFromUrl(imageLink, index.ToString(), imagePath, ext,
                        cancellationToken: cancellationToken);
                    RecordDownloadResult(_genericHttpStrategy, $"{imageLink.Url}{fullFilename}", result);
                    if (result.Outcome == DownloadOutcome.Success)
                    {
                        await PostProcessFileLevel(imageLink, imagePath, filesHashes, downloadStats, index, cancellationToken);
                    }
                    else if (result.Outcome == DownloadOutcome.Failed)
                    {
                        // The file exists under this extension but couldn't be downloaded; another extension won't help
                        downloadStats.FailedDownloads.Add($"{imageLink.Url}{fullFilename}");
                    }

                    break;
                }
                // A 404 while guessing means "not this extension"; anything else is a real error and propagates
                catch (WrongExtensionException)
                {
                    if (i == extensions.Length - 1)
                    {
                        downloadStats.FailedDownloads.Add($"{imageLink.Url}{index}");
                        RecordFailure(FailureStage.Download, _genericHttpStrategy.GetType().Name,
                            $"{imageLink.Url}{index}", FailureKind.NotFound, "Not found under any known extension");
                        Logger.Warning("Image not found under any known extension: {Url}{Index}", imageLink.Url, index);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Delegates the entire rip to whichever registered <see cref="IExternalToolDownloadStrategy"/> matches the current
    /// site (e.g. gallery-dl for DeviantArt).
    /// </summary>
    /// <exception cref="RipperException">No strategy is registered for the current site.</exception>
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
            var url = FolderInfo.Urls[0].Url;
            RecordFailure(FailureStage.Download, strategy.GetType().Name, url, result.FailureKind ?? FailureKind.Other,
                result.Reason);
            Logger.Error("External tool download failed for {Url}: {Reason}", url,
                result.Reason ?? "no details reported");
            downloadStats.FailedDownloads.Add(url);
        }
    }

    /// <summary>
    ///     Downloads every <see cref="FileLink"/> in <see cref="RipInfo.Urls"/>, either sequentially or
    ///     concurrently (per <see cref="RipInfo.MaxConcurrentDownloads"/>), refreshing links and resuming
    ///     from the failure point whenever a <see cref="UrlExpiredException"/> is thrown.
    /// </summary>
    /// <exception cref="RipperException">The parser for the expired site doesn't support link refreshing.</exception>
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
                var parser = HtmlParserOrchestrator.GetParser(e.SiteName, WebDriver, RequestHeaders, FilenameScheme);
                if (parser is not TimeSensitiveHtmlParser timeSensitiveParser)
                {
                    throw new RipperException($"Parser for {e.SiteName} does not support link refreshing");
                }

                start = e.ResumeIndex;
                FolderInfo.Urls = await timeSensitiveParser.UpdateLinks(FolderInfo.Urls, start, GivenUrl, cancellationToken);
            }
        }
    }

    /// <summary>Downloads all items with up to <paramref name="maxConcurrent"/> in flight at once, retrying each item independently up to 4 attempts before recording it as failed.</summary>
    /// <param name="maxConcurrent">The maximum number of concurrent downloads.</param>
    /// <param name="completed">An array indicating which downloads have been completed.</param>
    /// <param name="fullPath">The full path where files will be saved.</param>
    /// <param name="filesHashes">The hashes of the files being downloaded.</param>
    /// <param name="downloadStats">The statistics of the download process.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
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

    /// <summary>Downloads all items one at a time, starting from <paramref name="start"/>.</summary>
    /// <param name="start">The index to start downloading from.</param>
    /// <param name="fullPath">The full path where files will be saved.</param>
    /// <param name="filesHashes">The hashes of the files being downloaded.</param>
    /// <param name="downloadStats">The statistics of the download process.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
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

    /// <summary>
    ///     Downloads one item from the list, running post-processing on success and translating known
    ///     failure exceptions into recorded/resumable state before rethrowing.
    /// </summary>
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
            if (result.Outcome is DownloadOutcome.Success or DownloadOutcome.SkipNotAFailure)
            {
                lock (_finishedDownloadsLock)
                {
                    _finishedDownloads.Add(index);
                }
            }

            if (result.Outcome == DownloadOutcome.Success)
            {
                // DownloadFromList may modify filename (if it was missing extension)
                imagePath = Path.Combine(fullPath, link.Filename);
                await PostProcessFileLevel(link, imagePath, filesHashes, downloadStats, index, cancellationToken);
            }
        }
        catch (FileNotFoundException)
        {
            Logger.Warning("File not found: {Url} (referer: {Referer})", link.Url, link.Referer);
            downloadStats.FailedDownloads.Add(link.Url);
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

    /// <summary>
    /// Persists the current rip's URL and <see cref="CurrentIndex"/> so it can be resumed later via <see cref="GetStartIndex"/>.
    /// </summary>
    internal Task SaveCurrentRipPosition(CancellationToken cancellationToken = default)
    {
        return CacheUtility.SaveRipIndex(RipIndexPath, GivenUrl, CurrentIndex, cancellationToken);
    }

    /// <summary>
    /// Advances the saved rip-index position by one, without needing an active <see cref="FileRipper"/> instance.
    /// </summary>
    internal static async Task IncrementCurrentRipPosition(CancellationToken cancellationToken = default)
    {
        var (url, index) = await CacheUtility.ReadRipIndex(RipIndexPath, cancellationToken);
        index++;
        await CacheUtility.SaveRipIndex(RipIndexPath, url, index, cancellationToken);
    }

    /// <summary>
    /// Applies <see cref="PostDownloadAction"/>s to a successfully-downloaded file, skipping directory downloads and
    /// any <see cref="LinkInfo"/> whose strategy opts out via <see cref="IFileDownloadStrategy.SupportsPostProcessing"/>.
    /// </summary>
    private async Task PostProcessFileLevel(FileLink link, string imagePath, IndexedHashes filesHashes,
                                   DownloadStats downloadStats, int index,
                                   CancellationToken cancellationToken = default)
    {
        var fileLevelActions = PostDownloadAction.FileLevel;
        if (fileLevelActions.RemoveDuplicates)
        {
            // Directory downloads (e.g. a Mega folder link that couldn't be expanded into its files) aren't checked.
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
    
    private async Task PostProcessDirectoryLevel(string directoryPath, CancellationToken cancellationToken = default)
    {
        var directoryLevelActions = PostDownloadAction.DirectoryLevel;
        if (directoryLevelActions.RunPostProcessScript)
        {
            var siteConfig = Config.GetSiteConfig(SiteName);
            if (siteConfig is not null)
            {
                var scriptPath = siteConfig.PostProcessScript;
                if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
                {
                    Logger.Information("Running post-process script: {ScriptPath}", scriptPath);
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = scriptPath,
                        ArgumentList = { directoryPath },
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = new Process();
                    process.StartInfo = processStartInfo;
                    process.Start();

                    var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                    var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

                    await Task.WhenAll(outputTask, errorTask);

                    var output = await outputTask;
                    var error = await errorTask;

                    if (!string.IsNullOrEmpty(output))
                    {
                        Logger.Information("Post-process script output: {Output}", output);
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        Logger.Error("Post-process script error: {Error}", error);
                    }

                    await process.WaitForExitAsync(cancellationToken);
                }
                else
                {
                    Logger.Warning("Post-process script not found or path is empty: {ScriptPath}", scriptPath);
                }
            }
        }
    }

    /// <summary>Hashes the downloaded file and checks it against previously-seen hashes, deleting it if it's a duplicate.</summary>
    /// <returns>True if the file was a duplicate and has been deleted.</returns>
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
    ///     Downloads a single numerically-generated candidate URL/extension pair for <see cref="HandleGeneratingManually"/>,
    ///     temporarily rewriting <paramref name="fileLink"/>'s URL to the full candidate URL and restoring it afterward.
    /// </summary>
    /// <param name="fileLink">The base <see cref="FileLink"/> (URL trimmed to its directory) shared across all candidates for this album.</param>
    /// <param name="filename">The numeric index, as a string, used as this file's base name.</param>
    /// <param name="imagePath">Full path to download the file to.</param>
    /// <param name="ext">Candidate extension being tried.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the download operation.</param>
    private async Task<DownloadResult> DownloadFromUrl(FileLink fileLink, string filename, string imagePath, string ext,
                                                       CancellationToken cancellationToken = default)
    {
        var numFiles = FolderInfo.NumUrls;
        var url = fileLink.Url;
        var fullFilename = $"{filename}{ext}";
        var ripUrl = $"{url}{fullFilename}";
        var numProgress = $"({filename}/{numFiles})";
        Logger.Information("{RipUrl:l}    {NumProgress:l}", ripUrl, numProgress);
        fileLink.Url = ripUrl;

        DownloadResult result;
        try
        {
            result = await _genericHttpStrategy.DownloadAsync(fileLink, imagePath, DownloadContext, cancellationToken);
        }
        catch (Exception e) when (IsFailureToRecord(e))
        {
            RecordFailure(FailureStage.Download, _genericHttpStrategy.GetType().Name, ripUrl, FailedUrlLog.Classify(e),
                e.Message);
            throw;
        }
        finally
        {
            fileLink.Url = url;
        }

        await Sleep(50, cancellationToken);
        return result;
    }

    /// <summary>
    ///     Downloads one <see cref="FileLink"/> from the rip's list by dispatching to the registered
    ///     <see cref="IFileDownloadStrategy"/> for its <see cref="LinkInfo"/>, applying/restoring the referer
    ///     header around the attempt and recording or throwing on failure per <see cref="GeneralConfig.SkipFailedDownloads"/>.
    /// </summary>
    /// <param name="link"><see cref="FileLink"/> containing data on the file to download.</param>
    /// <param name="imagePath">Full path of the location to save the file to.</param>
    /// <param name="currentFileNum">Number of the file being downloaded, for progress logging.</param>
    /// <param name="downloadStats">Stats object to update with the result.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the download operation.</param>
    /// <exception cref="RipperException">The download failed and <see cref="GeneralConfig.SkipFailedDownloads"/> is false.</exception>
    private async Task<DownloadResult> DownloadFromList(FileLink link, string imagePath, int currentFileNum,
                                                        DownloadStats downloadStats,
                                                        CancellationToken cancellationToken = default)
    {
        var numFiles = FolderInfo.NumUrls;
        var ripUrl = link.Url;
        var displayUrl = link.LinkInfo == LinkInfo.Base64 ? Sdk.Utility.UrlUtility.TruncateLongUrl(ripUrl) : ripUrl;
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
        DownloadResult result;
        try
        {
            result = await strategy.DownloadAsync(link, imagePath, DownloadContext, cancellationToken);
        }
        catch (Exception e) when (IsFailureToRecord(e))
        {
            RecordFailure(FailureStage.Download, strategy.GetType().Name, ripUrl, FailedUrlLog.Classify(e), e.Message);
            throw;
        }

        RecordDownloadResult(strategy, ripUrl, result);
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

        RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
        await Sleep(50, cancellationToken);

        return result;
    }

    /// <summary>
    /// Releases the held <see cref="WebDriver"/> back to the pool and disposes the HTTP session. Safe to call more than once.
    /// </summary>
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

    ~FileRipper()
    {
        Dispose();
    }
}