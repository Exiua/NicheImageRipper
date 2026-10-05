using NicheImageRipper.Core.FileDownloading;
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

    private WebDriver WebDriver { get; }
    public bool Interrupted { get; set; }
    private string SiteName { get; set; }
    public float SleepTime { get; set; }
    public float Jitter { get; set; }
    private int RetryCount { get; set; } = 4;
    private string GivenUrl { get; set; }
    private FilenameScheme FilenameScheme { get; }
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
                                  FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        HttpClient = new HttpClient();
        WebDriver = driver;
        RequestHeaders = requestHeaders;
        FilenameScheme = filenameScheme;
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
        (SiteName, SleepTime) = Core.Utility.UrlUtility.SiteCheck(GivenUrl, RequestHeaders);

        // Site must be known before the parser is constructed — HtmlParserFactory.Create resolves the
        // concrete type from SiteName, so building the parser has to happen after SiteCheck, not before.
        var htmlParser = GetParser(SiteName, WebDriver, RequestHeaders, FilenameScheme);
        htmlParser.GivenUrl = GivenUrl;
        htmlParser.SiteName = SiteName;
        Logger.Debug("Constructed HtmlParser");

        if (htmlParser.SupportsPartialSaveValue)
        {
            var saveData = PartialSaveManager.GetPartialSave(url);
            if (saveData is not null)
            {
                Logger.Debug("Partial save found for {Url}", url);
                RequestHeaders[RequestHeaderKeys.Cookie] = saveData.Cookies;
                RequestHeaders[RequestHeaderKeys.Referer] = saveData.Referer;
                Interrupted = true;
                return saveData.RipInfo;
            }
        }
        else
        {
            File.Delete(ImageRipper.RipIndexPath); // Not valid when no partial save
        }

        Logger.Debug("No partial save found for site; Parsing site");
        if (htmlParser.RequiresNavigationValue)
        {
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

    private async Task CleanupWhenFailed(Exception e, CancellationToken cancellationToken = default)
    {
        Driver.SwitchTo().DefaultContent();
        Logger.Error(e, "Failed to parse {CurrentUrl}", CurrentUrl);
        #if DEBUG
        await File.WriteAllTextAsync("test.html", Driver.PageSource, cancellationToken);
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

    private static readonly Dictionary<string, string> TestSiteNameOverrides = new()
    {
        ["x-x-x"] = "xxxtube",
        ["twitter"] = "x",
    };

    /// <summary>
    /// Dev/test-only entry point: parses a URL by resolving and running a single parser directly, bypassing
    /// <see cref="ImageRipper"/> entirely. Uses a looser site-detection path than production (no whitelist
    /// gate), so it can exercise parsers for sites not yet enabled in <c>UrlUtility.SiteCheck</c>.
    /// </summary>
    public async Task<RipInfo> TestParse(string givenUrl, bool debug, bool printSite,
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
            JsonUtility.Serialize("test.json", outData);
            if (debug)
            {
                NicheImageRipper.LogMessageToFile("Press any key to exit...", LogEventLevel.Debug);
                Console.ReadKey();
            }

            return data;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error occurred while testing {SiteName}Parser", SiteName);
            await File.WriteAllTextAsync("test.html", Driver.PageSource, cancellationToken);
            Driver.TakeDebugScreenshot();
            Driver.DumpCookies();
            throw;
        }
        finally
        {
            if (printSite)
            {
                await File.WriteAllTextAsync("test.html", Driver.PageSource, cancellationToken);
            }
        }
    }

    private Task<RipInfo> EvaluateParser(string siteName, CancellationToken cancellationToken = default)
    {
        if (TestSiteNameOverrides.TryGetValue(siteName, out var overrideName))
        {
            siteName = overrideName;
        }

        Logger.Debug("Resolving parser for site: {SiteName}", siteName);
        var parser = HtmlParserFactory.Create(siteName, WebDriver, RequestHeaders, FilenameScheme);
        parser.GivenUrl = CurrentUrl;
        parser.SiteName = siteName;
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