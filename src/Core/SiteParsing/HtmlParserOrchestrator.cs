using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.GalleryUpdates;
using NicheImageRipper.Core.PartialSaves;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.Managers;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.Sdk.Utility;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Manager;
using Serilog;
using Serilog.Events;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing;

/// <summary>
/// Drives the shared parse pipeline for a constructed <see cref="HtmlParser"/>: site detection,
/// partial-save caching, retry-on-WebDriverException, and a small dev/test harness for exercising
/// a single parser directly. Owns no scraping logic itself — that lives on <see cref="HtmlParser"/>.
/// </summary>
public class HtmlParserOrchestrator : IDisposable
{
    private static GeneralConfig Config => Sdk.Configuration.Config.Instance;

    private static PartialSaveManager PartialSaveManager => PartialSaveManager.Instance;

    private static GalleryStateManager GalleryStateManager => GalleryStateManager.Instance;

    /// <summary>
    ///     Set by <see cref="ParseSite"/> when the site's parser is an <see cref="IUpdatableHtmlParser"/>: how this rip
    ///     merges into what earlier rips downloaded. The returned <see cref="RipInfo.Urls"/> are its downloads.
    /// </summary>
    public GalleryUpdatePlan? UpdatePlan { get; private set; }

    /// <summary>Type name of the parser <see cref="ParseSite"/> chose; null until one has been constructed.</summary>
    public string? ParserTypeName { get; private set; }

    private WebDriver WebDriver { get; }
    public bool Interrupted { get; set; }
    private string SiteName { get; set; }
    public float SleepTime { get; set; }
    public float Jitter { get; set; }
    private int RetryCount { get; set; } = 4;
    private string GivenUrl { get; set; }
    private FilenameScheme FilenameScheme { get; }

    /// <summary>The parser that delegated this parse via <see cref="HtmlParser.ParseWith"/>; null for a top-level rip.</summary>
    private HtmlParser? Caller { get; }
    private Dictionary<string, string> RequestHeaders { get; }
    private ILogger Logger { get; init; }
    private HttpClient HttpClient { get; set; }

    private FirefoxDriver Driver => WebDriver.Driver;

    private string CurrentUrl
    {
        get => Driver.Url;
        set => Driver.Url = value;
    }

    private static FlareSolverrManager FlareSolverrManager => NicheImageRipper.FlareSolverrManager;

    public HtmlParserOrchestrator(WebDriver driver, Dictionary<string, string> requestHeaders,
                                  FilenameScheme filenameScheme = FilenameScheme.Original,
                                  HtmlParser? caller = null)
    {
        HttpClient = RateLimitingHandler.CreateClient();
        WebDriver = driver;
        RequestHeaders = requestHeaders;
        FilenameScheme = filenameScheme;
        Caller = caller;
        Interrupted = false;
        SiteName = "";
        SleepTime = 0.2f;
        Jitter = 0.5f;
        GivenUrl = "";
        Logger = Log.ForContext<HtmlParserOrchestrator>();
    }

    /// <summary>
    /// Parses the given URL into a <see cref="RipInfo"/>: normalizes it, detects the site, constructs
    /// that site's parser, returns a cached partial save if one exists and the parser supports partial
    /// saves, otherwise navigates (if required) and runs <see cref="HtmlParser.Parse"/>, retrying on
    /// <see cref="WebDriverException"/> up to <see cref="RetryCount"/> times and caching the result on success.
    /// </summary>
    /// <param name="url">The page/gallery URL to parse.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The parsed <see cref="RipInfo"/>.</returns>
    /// <exception cref="RipperException"><see cref="RetryCount"/> is less than 1, or parsing failed after all retries.</exception>
    public async Task<RipInfo> ParseSite(string url, CancellationToken cancellationToken = default)
    {
        Logger.Debug("Parsing {Url}", url);
        url = UrlUtility.NormalizeUrl(url);
        GivenUrl = url;
        (SiteName, SleepTime) = Utility.UrlUtility.SiteCheck(GivenUrl, RequestHeaders);

        // Site must be known before the parser is constructed — HtmlParserFactory.Create resolves the
        // concrete type from SiteName, so building the parser has to happen after SiteCheck, not before.
        ParserTypeName = null;
        var htmlParser = GetParser(SiteName, WebDriver, RequestHeaders, FilenameScheme);
        ParserTypeName = htmlParser.GetType().Name;
        htmlParser.GivenUrl = GivenUrl;
        htmlParser.SiteName = SiteName;
        if (Caller is null)
        {
            htmlParser.GalleryUpdateMode = Config.GetGalleryUpdateMode(GetParserName(htmlParser));
        }
        else
        {
            ApplySubParserOverrides(htmlParser, Caller);
        }

        Logger.Debug("Constructed HtmlParser");

        UpdatePlan = null;
        var updatableParser = htmlParser as IUpdatableHtmlParser;
        var galleryState = updatableParser is null ? null : GalleryStateManager.GetState(url);

        // An updatable gallery's partial save is only used to resume an unfinished update. Otherwise it's the link
        // list from the last rip, which would hide everything added since
        var canResume = updatableParser is null || galleryState?.PendingUpdate is not null;
        if (htmlParser.SupportsPartialSaveValue && canResume)
        {
            var saveData = PartialSaveManager.GetPartialSave(url);
            if (saveData is not null)
            {
                Logger.Debug("Partial save found for {Url}", url);
                RequestHeaders[RequestHeaderKeys.Cookie] = saveData.Cookies;
                RequestHeaders[RequestHeaderKeys.Referer] = saveData.Referer;
                Interrupted = true;
                UpdatePlan = galleryState?.PendingUpdate;
                return saveData.RipInfo;
            }
        }

        if (!htmlParser.SupportsPartialSaveValue || updatableParser is not null)
        {
            File.Delete(FileRipper.RipIndexPath); // Not valid when no partial save, or for a freshly planned update
        }

        var updateMode = updatableParser is null ? GalleryUpdateMode.Full : htmlParser.GalleryUpdateMode;
        var updateStrategy = updatableParser is null ? default : ResolveUpdateStrategy(htmlParser);
        if (updatableParser is not null)
        {
            updatableParser.UpdateContext = galleryState is { Items.Count: > 0 } && updateMode == GalleryUpdateMode.UpdatesOnly
                ? new GalleryUpdateContext(updateStrategy, galleryState.Cursor,
                    galleryState.Items.Select(i => i.Key).ToHashSet())
                : null;
        }

        Logger.Debug("No partial save found for site; Parsing site");
        if (htmlParser.RequiresNavigationValue)
        {
            await DomainRateLimiter.WaitAsync(url, cancellationToken);
            CurrentUrl = url;
        }

        if (htmlParser.RequiresLoginValue)
        {
            await htmlParser.SiteLogin(cancellationToken);
        }

        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                Logger.Debug("Executing parser for {SiteName}", SiteName);
                var siteInfo = await htmlParser.Parse(cancellationToken);
                Logger.Debug("Saving partial save for {Url}", url);
                siteInfo.DirectoryName += $" ({SiteName})";
                if (updatableParser is not null)
                {
                    siteInfo = PlanUpdate(updatableParser, updateStrategy, galleryState, updateMode, siteInfo, url);
                }

                WritePartialSave(siteInfo, url);
                return siteInfo;
            }
            catch (WebDriverException e)
            {
                if (attempt < RetryCount - 1)
                {
                    Logger.Warning(e, "Attempt {Attempt} failed due to WebDriver, retrying...", attempt + 1);
                    await Sleep(250, cancellationToken);
                    continue;
                }

                await CleanupWhenFailed(e, cancellationToken);
                throw;
            }
            catch (Exception e)
            {
                await CleanupWhenFailed(e, cancellationToken);
                throw;
            }
        }

        // Can only reach here if RetryCount is less than 1 as the loop would have returned or thrown
        throw new RipperException("Retry count cannot be less than 1");
    }

    /// <summary>
    ///     Merges a parsed updatable gallery with what earlier rips stored and records the plan as pending. Returns the
    ///     <see cref="RipInfo"/> to download: only what changed, unless <paramref name="mode"/> is full.
    /// </summary>
    private RipInfo PlanUpdate(IUpdatableHtmlParser parser, GalleryUpdateStrategy strategy, GalleryState? state,
                               GalleryUpdateMode mode, RipInfo siteInfo, string url)
    {
        if (siteInfo.DownloadMode != DownloadMode.List)
        {
            Logger.Debug("Not tracking updates for {Url}: download mode {Mode} has no item list", url,
                siteInfo.DownloadMode);
            return siteInfo;
        }

        // Keep updates in the folder earlier rips used, even if the site renamed the gallery
        if (state is not null && state.DirectoryName != siteInfo.DirectoryName)
        {
            Logger.Warning("The gallery at {Url} is now named \"{NewName}\"; still saving to \"{DirectoryName}\"",
                url, siteInfo.DirectoryName, state.DirectoryName);
            siteInfo.DirectoryName = state.DirectoryName;
        }

        var parsed = siteInfo.Urls
                             .Select(link => new ParsedGalleryItem(parser.GetItemKey(link), parser.GetItemVersion(link), link))
                             .ToList();
        var plan = GalleryUpdatePlanner.Plan(state?.Items ?? [], parsed, strategy,
            parser.NewItemPlacement, mode, siteInfo.FilenameScheme, siteInfo.DirectoryName, siteInfo.UpdateCursor);
        GalleryStateManager.SavePendingUpdate(url, plan);
        UpdatePlan = plan;
        Logger.Debug("Update plan for {Url}: {Downloads} to download, {Renames} to rename", url,
            plan.PendingItems.Count, plan.Renames.Count);

        siteInfo.Urls = plan.Downloads;
        siteInfo.NumUrls = plan.Downloads.Count;
        return siteInfo;
    }

    /// <summary>The site config's update strategy if the parser supports it, otherwise the parser's default.</summary>
    private GalleryUpdateStrategy ResolveUpdateStrategy(HtmlParser parser)
    {
        var support = GalleryUpdateSupport.For(parser.GetType())!;
        var parserName = GetParserName(parser);
        var requested = Caller is null
            ? Config.GetGalleryUpdateStrategy(parserName)
            : Config.GetSubParserGalleryUpdateStrategy(parserName);
        if (requested is { } strategy && !support.IsSupported(strategy))
        {
            Logger.Warning("{Parser} doesn't support the {Strategy} update strategy; using {Default}",
                parser.GetType().Name, strategy, support.Default);
        }

        return support.Resolve(requested);
    }

    private static string GetParserName(HtmlParser parser) =>
        parser.GetType().GetProperty(nameof(IHtmlParser.ParserName))?.GetValue(null) as string ?? parser.SiteName;

    /// <summary>
    ///     Replaces the overridable settings <paramref name="parser"/> took from its own site config with those
    ///     <see cref="GeneralConfig.SubParserOverrides"/> resolves for a parser <paramref name="caller"/> delegated to.
    /// </summary>
    internal static void ApplySubParserOverrides(HtmlParser parser, HtmlParser caller)
    {
        var parserName = GetParserName(parser);
        parser.FilenameScheme = Config.GetSubParserFilenameScheme(parserName, caller.FilenameScheme);
        parser.GalleryUpdateMode = Config.GetSubParserGalleryUpdateMode(parserName, caller.GalleryUpdateMode);
    }

    private async Task CleanupWhenFailed(Exception e, CancellationToken cancellationToken = default)
    {
        Driver.SwitchTo().DefaultContent();
        Logger.Error(e, "Failed to parse {CurrentUrl}", CurrentUrl);
        #if DEBUG
        await File.WriteAllTextAsync(AppData.GetPath("test.html"), Driver.PageSource, cancellationToken);
        Driver.TakeDebugScreenshot();
        #endif
    }

    public static HtmlParser GetParser(string siteName, WebDriver webDriver,
                                       Dictionary<string, string> requestHeaders,
                                       FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        return HtmlParserFactory.Create(siteName, webDriver, requestHeaders, filenameScheme);
    }

    private void WritePartialSave(RipInfo ripInfo, string url)
    {
        var partialSaveEntry = new PartialSaveEntry
        {
            Cookies = RequestHeaders[RequestHeaderKeys.Cookie],
            Referer = RequestHeaders[RequestHeaderKeys.Referer],
            RipInfo = ripInfo
        };
        PartialSaveManager.AddPartialSave(url, partialSaveEntry);
    }

    private static Task Sleep(int milliseconds, CancellationToken cancellationToken = default)
    {
        return Task.Delay(milliseconds, cancellationToken);
    }

    #region Parser Testing

    /// <summary>
    /// Dev/test-only entry point: parses a URL by resolving and running a single parser directly, bypassing
    /// <see cref="FileRipper"/> entirely. Uses a looser site-detection path than production (no whitelist
    /// gate), so it can exercise parsers for sites not yet enabled in <c>UrlUtility.SiteCheck</c>.
    /// </summary>
    public async Task<RipInfo> TestParse(string givenUrl, bool printSite,
                                         CancellationToken cancellationToken = default)
    {
        try
        {
            OpenQA.Selenium.Internal.Logging.Log.SetLevel(
                typeof(OpenQA.Selenium.Remote.RemoteWebDriver),
                OpenQA.Selenium.Internal.Logging.LogEventLevel.Trace);
            OpenQA.Selenium.Internal.Logging.Log.SetLevel(
                typeof(SeleniumManager),
                OpenQA.Selenium.Internal.Logging.LogEventLevel.Trace);

            var supportedUrls = HtmlParserFactory.SupportedUrls;
            Logger.Debug("Supported URLs: ");
            foreach(var url in supportedUrls)
            {
                Logger.Debug("\t- {Url}", url);
            }
            
            CurrentUrl = UrlUtility.NormalizeUrl(givenUrl);
            SiteName = TestSiteCheck(CurrentUrl);

            Logger.Debug("Testing: {SiteName}Parser", SiteName);
            Logger.Debug("URL: {CurrentUrl}", CurrentUrl);
            var start = DateTime.Now;
            var data = await EvaluateParser(SiteName, cancellationToken);
            var end = DateTime.Now;
            if (data.Urls.Count == 0)
            {
                Logger.Error("No URLs found for {SiteName}Parser", SiteName);
            }
            else
            {
                Logger.Debug("Referer: {Referer}", data.Urls[0].Referer);
            }

            Logger.Debug("Time Elapsed: {TimeElapsed}", end - start);
            var outData = data.Urls.Select(d => d.Url).ToList();
            JsonUtility.Serialize(AppData.GetPath("test.json"), outData);
            return data;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error occurred while testing {SiteName}Parser", SiteName);
            await File.WriteAllTextAsync(AppData.GetPath("test.html"), Driver.PageSource, cancellationToken);
            Driver.TakeDebugScreenshot();
            Driver.DumpCookies();
            throw;
        }
        finally
        {
            if (printSite)
            {
                await File.WriteAllTextAsync(AppData.GetPath("test.html"), Driver.PageSource, cancellationToken);
            }
        }
    }

    private Task<RipInfo> EvaluateParser(string siteName, CancellationToken cancellationToken = default)
    {
        // Parsers declare every name they answer to (ParserName plus AdditionalParserNames), so no aliasing here
        Logger.Debug("Resolving parser for site: {SiteName}", siteName);
        var parser = HtmlParserFactory.Create(siteName, WebDriver, RequestHeaders, FilenameScheme);
        parser.GivenUrl = CurrentUrl;
        parser.SiteName = siteName;
        parser.GalleryUpdateMode = Config.GetGalleryUpdateMode(GetParserName(parser));
        return parser.Parse(cancellationToken);
    }

    private string TestSiteCheck(string url)
    {
        return Core.Utility.UrlUtility.ExtractDomainAndSetReferer(url, RequestHeaders);
    }

    #endregion

    public void Dispose()
    {
        if (Config.CloseFlareSolverrSession)
        {
            FlareSolverrManager.DeleteSession().Wait();
        }

        DisposeInternal();

        GC.SuppressFinalize(this);
    }

    protected virtual void DisposeInternal()
    {
        HttpClient.Dispose();
    }
}