using FlareSolverrIntegration.Responses;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.BiDi;
using OpenQA.Selenium.Firefox;
using Sdk.Common;
using Sdk.Common.ExtensionMethods;
using Sdk.Configuration;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.Exceptions;
using Sdk.Managers;
using Serilog;
using WebDriver = Sdk.Driver.WebDriver;

namespace Sdk.SiteParsing;

/// <summary>
///     Base class for all site-specific HTML parsers. Handles the shared parse pipeline (site detection,
///     partial-save caching, retry-on-WebDriverException), scraping helpers (Soupify/lazy-load/FlareSolverr),
///     and a small dev/test harness for exercising a single parser directly.
/// </summary>
public abstract class HtmlParser : IDisposable
{
    protected const string Protocol = "https:";

    protected static IReadOnlySet<string> ExternalSites { get; } //=> HtmlParserFactory.DelegatableDomains;

    protected static GeneralConfig Config => Configuration.Config.Instance;

    protected WebDriver WebDriver { get; }
    public bool Interrupted { get; set; }
    private string SiteName { get; set; }
    public float SleepTime { get; set; }
    public float Jitter { get; set; }
    public int RetryCount { get; set; } = 4;
    protected string GivenUrl { get; set; }
    protected FilenameScheme FilenameScheme { get; }
    protected Dictionary<string, string> RequestHeaders { get; }
    protected ApiClientManager ApiClientManager { get; }
    protected ILogger Logger { get; init; }
    protected HttpClient HttpClient { get; set; }

    protected FirefoxDriver Driver => WebDriver.Driver;

    protected string CurrentUrl
    {
        get => Driver.Url;
        set => Driver.Url = value;
    }

    /// <summary>
    /// Whether <see cref="ParseSite"/> should navigate the driver to the given URL before calling <see cref="Parse"/>.
    /// False for parsers (e.g. <c>AllBooruParser</c>) that fetch data directly via HttpClient and never touch the driver.
    /// </summary>
    protected virtual bool RequiresNavigation => true;

    /// <summary>
    /// Whether <see cref="ParseSite"/> should check/write a cached <see cref="RipInfo"/> for this parser's site. False
    /// for sites whose links expire too quickly to cache (e.g. e-hentai).
    /// </summary>
    protected virtual bool SupportsPartialSave => true;
    
    /// <summary>
    /// Whether this parser needs SiteLogin run before ParseCore. Checked on every entry path
    /// (both ParseSite-driven and delegated Parse(url) calls), since delegated calls bypass ParseSite entirely.
    /// </summary>
    protected virtual bool RequiresLogin => false;

    protected static FlareSolverrManager FlareSolverrManager { get; } //=> NicheImageRipper.FlareSolverrManager;

    protected HtmlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        HttpClient = new HttpClient();
        WebDriver = driver;
        ApiClientManager = clientManager;
        RequestHeaders = requestHeaders;
        FilenameScheme = filenameScheme;
        Interrupted = false;
        SiteName = "";
        SleepTime = 0.2f;
        Jitter = 0.5f;
        GivenUrl = "";
        Logger = Log.ForContext<HtmlParser>();
    }

    // Only called by self and ParameterizedHtmlParser
    protected Task<bool> SiteLogin(CancellationToken cancellationToken = default)
    {
        Logger.Debug("Checking if already logged in to {SiteName}", SiteName);
        if (IsLoggedInToSite(SiteName))
        {
            Logger.Debug("Already logged in to {SiteName}", SiteName);
            return Task.FromResult(true);
        }

        Logger.Debug("Logging in to {SiteName}", SiteName);
        var loginTask = SiteLoginHelper(cancellationToken);

        return loginTask.ContinueWith(task =>
        {
            WebDriver.SiteLoginStatus[SiteName] = task.Result;
            Logger.Debug("Logged in to {SiteName}: {Result}", SiteName, task.Result);
            return task.Result;
        }, cancellationToken);
    }

    protected virtual Task<bool> SiteLoginHelper(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    private bool IsLoggedInToSite(string siteName)
    {
        var siteLoginStatus = WebDriver.SiteLoginStatus;
        return !siteLoginStatus.TryAdd(siteName, false) && siteLoginStatus[siteName];
    }

    public abstract Task<RipInfo> Parse(CancellationToken cancellationToken = default);

    /// <summary>Convert current page into an HtmlNode object</summary>
    /// <param name="delay">How long to wait after loading the page (in milliseconds) before parsing</param>
    /// <param name="lazyLoadArgs">Arguments for lazy loading elements on the page</param>
    /// <param name="xpath">XPath of an element to wait for before parsing</param>
    /// <param name="xpathTimeout">Timeout (in seconds) for waiting for the XPath element</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>Parsed HtmlNode object</returns>
    protected async Task<HtmlNode> Soupify(int delay = 0, LazyLoadArgs? lazyLoadArgs = null, string xpath = "",
                                           int xpathTimeout = 10, CancellationToken cancellationToken = default)
    {
        if (delay > 0)
        {
            await Task.Delay(delay, cancellationToken);
        }

        if (xpath != "")
        {
            await WaitForElement(xpath, timeout: xpathTimeout, cancellationToken: cancellationToken);
        }

        if (lazyLoadArgs is not null)
        {
            await LazyLoad(lazyLoadArgs, cancellationToken);
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(Driver.PageSource);
        return doc.DocumentNode;
    }

    /// <summary>Convert input into an HtmlNode object</summary>
    /// <param name="url">URL or HTML string. If a url is provided, the driver will navigate to it first, before parsing the page.</param>
    /// <param name="delay">How long to wait after loading the page (in milliseconds) before parsing</param>
    /// <param name="lazyLoadArgs">Arguments for lazy loading elements on the page</param>
    /// <param name="xpath">XPath of an element to wait for before parsing</param>
    /// <param name="urlString">Indicates whether the 'url' parameter is a URL (true) or an HTML string (false)</param>
    /// <param name="cookies">Cookies to add before loading the page</param>
    /// <param name="xpathTimeout">Timeout (in seconds) for waiting for the XPath element</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>Parsed HtmlNode object</returns>
    protected async Task<HtmlNode> Soupify(string url, int delay = 0, LazyLoadArgs? lazyLoadArgs = null,
                                           string xpath = "", bool urlString = true, ICookieJar? cookies = null,
                                           int xpathTimeout = 10, CancellationToken cancellationToken = default)
    {
        if (!urlString)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(url);
            return doc.DocumentNode;
        }

        CurrentUrl = url;

        if (cookies is not null)
        {
            var cookieJar = Driver.GetCookieJar();
            foreach (var cookie in cookies.AllCookies)
            {
                cookieJar.AddCookie(cookie);
            }
        }

        return await Soupify(delay: delay, lazyLoadArgs: lazyLoadArgs, xpath: xpath, xpathTimeout: xpathTimeout,
            cancellationToken: cancellationToken);
    }

    /// <summary>Convert HttpResponseMessage content into an HtmlNode object</summary>
    /// <param name="response">HttpResponseMessage to parse</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>Parsed HtmlNode object</returns>
    protected static async Task<HtmlNode> Soupify(HttpResponseMessage response,
                                                  CancellationToken cancellationToken = default)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(content);
        return htmlDocument.DocumentNode;
    }

    /// <param name="baseResponse">Response to parse</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>Parsed HtmlNode object</returns>
    /// <exception cref="RipperException">The response was an error, or an unexpected response type.</exception>
    protected async Task<HtmlNode> Soupify(CSWebDriverClient.Models.Responses.BaseResponse baseResponse,
                                           CancellationToken cancellationToken = default)
    {
        return baseResponse switch
        {
            CSWebDriverClient.Models.Responses.ErrorResponse errorResponse =>
                errorResponse.Details is not null
                    ? throw new RipperException($"{errorResponse.Error}: {errorResponse.Details}")
                    : throw new RipperException(errorResponse.Error),
            CSWebDriverClient.Models.Responses.PageResponse pageResponse => await Soupify(pageResponse.Content,
                urlString: false, cancellationToken: cancellationToken),
            CSWebDriverClient.Models.Responses.GetNetworkUrlsResponse =>
                throw new RipperException("Incorrect response type: GetNetworkUrlsResponse"),
            _ => throw new RipperException($"Unknown response type: {baseResponse}")
        };
    }

    /// <summary>Convert FlareSolverr Solution response into an HtmlNode object</summary>
    /// <param name="solution">FlareSolverr Solution to parse</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>Parsed HtmlNode object</returns>
    private static Task<HtmlNode> Soupify(Solution solution, CancellationToken cancellationToken = default)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(solution.Response);
        return Task.FromResult(htmlDocument.DocumentNode);
    }

    /// <summary>Wait for an element to exist on the page</summary>
    /// <param name="xpath">XPath of the element to wait for</param>
    /// <param name="delay">Delay between each check</param>
    /// <param name="timeout">Timeout (in seconds) for the wait (-1 for no timeout)</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>The tag name of the found element, or null if the timeout is reached</returns>
    protected async Task<string?> WaitForElement(string xpath, float delay = 0.1f, float timeout = 10,
                                                 CancellationToken cancellationToken = default)
    {
        var timeoutSpan = TimeSpan.FromSeconds(timeout);
        var startTime = DateTime.Now;
        var found = Driver.FindElements(By.XPath(xpath));
        while (found.Count == 0)
        {
            await Task.Delay((int)(delay * 1000), cancellationToken);
            var currTime = DateTime.Now;
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (timeout == -1)
            {
                continue; // No timeout, keep waiting
            }

            if (currTime - startTime >= timeoutSpan)
            {
                return null;
            }
        }

        var foundElement = found[0];
        return foundElement.TagName;
    }

    /// <summary>Close all tabs that do not contain the specified URL match.</summary>
    /// <param name="urlMatch">The substring that should be present in the URL of the tabs to keep open.</param>
    protected void CleanTabs(string urlMatch)
    {
        var windowHandles = Driver.WindowHandles;
        foreach (var handle in windowHandles)
        {
            Driver.SwitchTo().Window(handle);
            if (!CurrentUrl.Contains(urlMatch))
            {
                Driver.Close();
            }
        }

        Driver.SwitchTo().Window(Driver.WindowHandles[0]);
    }

    /// <summary>Solves a CAPTCHA using FlareSolverr, parses the returned HTML, and adds the necessary cookies to the browser session.</summary>
    /// <param name="regenerateSessionOnFailure">If true, regenerates the session and retries once if CAPTCHA solving fails.</param>
    /// <param name="cookies">Optional cookie dictionaries to include in the session when solving the CAPTCHA.</param>
    /// <param name="cookieWhitelist">List of cookie names to retain from the existing session.</param>
    /// <param name="replaceUserAgent">Whether to replace the User-Agent header with the one provided by FlareSolverr.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The parsed HTML document as an <see cref="HtmlNode"/>.</returns>
    /// <exception cref="FeatureNotAvailableException">FlareSolverr support is not available.</exception>
    /// <exception cref="FailedToGetSolutionException">CAPTCHA solving fails and session regeneration is disabled.</exception>
    protected async Task<HtmlNode> SolveParseAddCookies(bool regenerateSessionOnFailure = false,
                                                        List<Dictionary<string, string>>? cookies = null,
                                                        List<string>? cookieWhitelist = null,
                                                        bool replaceUserAgent = false,
                                                        CancellationToken cancellationToken = default)
    {
        var solution = await Solve(regenerateSessionOnFailure, cookies, cancellationToken);
        if (replaceUserAgent)
        {
            Logger.Debug("Replacing User-Agent with FlareSolverr provided User-Agent: {UserAgent}", solution.UserAgent);
            var currentUrl = CurrentUrl;
            WebDriver.RegenerateDriver(solution.UserAgent);
            CurrentUrl = currentUrl;
        }

        var cookieJar = Driver.GetCookieJar();
        foreach (var cookie in solution.Cookies.Where(cookie =>
                     cookieWhitelist is null || cookieWhitelist.Contains(cookie.Name)))
        {
            Logger.Debug("Adding cookie: {@Cookie}", cookie);
            var seleniumCookie = cookie.ToSeleniumCookie();
            cookieJar.SetCookie(seleniumCookie);
        }

        return await Soupify(solution, cancellationToken: cancellationToken);
    }

    /// <param name="regenerateSessionOnFailure">If true, regenerates the session and retries once if CAPTCHA solving fails.</param>
    /// <param name="cookies">Optional cookie dictionaries to include in the session when solving the CAPTCHA.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The parsed HTML document as an <see cref="HtmlNode"/>.</returns>
    protected async Task<HtmlNode> SolveParse(bool regenerateSessionOnFailure = false,
                                              List<Dictionary<string, string>>? cookies = null,
                                              CancellationToken cancellationToken = default)
    {
        var solution = await Solve(regenerateSessionOnFailure, cookies, cancellationToken);
        return await Soupify(solution, cancellationToken: cancellationToken);
    }

    private async Task<Solution> Solve(bool regenerateSessionOnFailure = false,
                                       List<Dictionary<string, string>>? cookies = null,
                                       CancellationToken cancellationToken = default)
    {
        // TODO: FIXME
        // if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.FlareSolverr))
        // {
        //     throw new FeatureNotAvailableException(ExternalFeatureSupport.FlareSolverr);
        // }

        // Safety: Solution will not be null unless all attempts fail in which case an exception is thrown.
        Solution solution = null!;
        for (var i = 0; i < 4; i++)
        {
            try
            {
                Logger.Debug("Attempting to get site solution for {CurrentUrl} (Attempt {Attempt})", CurrentUrl, i + 1);
                solution = await FlareSolverrManager.GetSiteSolution(CurrentUrl, cookies, cancellationToken);
                break;
            }
            catch (FailedToGetSolutionException)
            {
                if (i == 3)
                {
                    throw;
                }

                await Sleep(250, cancellationToken);
                Logger.Warning("Failed to get site solution for {CurrentUrl}, retrying...", CurrentUrl);
            }
        }

        #if DEBUG
        await File.WriteAllTextAsync("test-solver.html", solution.Response, cancellationToken);
        Logger.Debug("User-Agent: {UserAgent}", solution.UserAgent);
        #endif

        return solution;
    }

    public static Task JitterSleep(int min = 250, int max = 2500, CancellationToken cancellationToken = default)
    {
        var jitter = Random.Shared.Next(min, max);
        return Task.Delay(jitter, cancellationToken);
    }

    protected static Task Sleep(int milliseconds, CancellationToken cancellationToken = default)
    {
        return Task.Delay(milliseconds, cancellationToken);
    }

    protected static async Task<T> RetryUntil<T>(Func<Task<T>> func, Func<T, bool> successCondition,
                                                 string errorMessage, int delay = 250,
                                                 CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 4;
        T value = default!;
        for (var i = 0; i < maxAttempts; i++)
        {
            value = await func();
            if (!successCondition(value))
            {
                if (i == maxAttempts - 1)
                {
                    throw new RipperException(errorMessage);
                }

                await Sleep(delay, cancellationToken);
                continue;
            }

            break;
        }

        return value;
    }

    protected async Task<(T, IBiDi)> ConfigureNetworkCapture<T>(CancellationToken cancellationToken = default)
        where T : PlaylistCapturer, new()
    {
        var capturer = new T();
        var bidi = await Driver.AsBiDiAsync(cancellationToken: cancellationToken);
        await bidi.Network.ResponseCompleted.SubscribeAsync(capturer.CaptureHook, cancellationToken: cancellationToken);
        return (capturer, bidi);
    }

    /// <summary>Scrolls through the page to lazy load images</summary>
    /// <param name="args">Arguments for lazy loading</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    protected Task LazyLoad(LazyLoadArgs args, CancellationToken cancellationToken = default)
    {
        return args.StopElement is not null && Driver.TryFindElement(args.StopElement) is not null
            ? LazyLoad(args.StopElement, cancellationToken: cancellationToken)
            : LazyLoad(args.ScrollBy, args.Increment, args.ScrollPauseTime, args.ScrollBack, args.ReScroll,
                cancellationToken);
    }

    /// <summary>Scroll through the page to lazy load images</summary>
    /// <param name="scrollBy">Whether to scroll through the page or instantly scroll to the bottom</param>
    /// <param name="increment">Distance to scroll by each iteration</param>
    /// <param name="scrollPauseTime">Seconds to wait between each scroll</param>
    /// <param name="scrollBack">Distance to scroll back by after reaching the bottom of the page</param>
    /// <param name="rescroll">Whether scrolling through the page again</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    protected async Task LazyLoad(bool scrollBy = false, int increment = 2500, int scrollPauseTime = 500,
                                  int scrollBack = 0, bool rescroll = false,
                                  CancellationToken cancellationToken = default)
    {
        var lastHeight = Driver.GetScrollHeight();
        if (rescroll)
        {
            Driver.ExecuteScript("window.scrollTo(0, 0);");
        }

        string scrollScript;
        string heightCheckScript;
        if (scrollBy)
        {
            scrollScript = $"window.scrollBy({{top: {increment}, left: 0, behavior: 'smooth'}});";
            heightCheckScript = "return window.pageYOffset";
        }
        else
        {
            scrollScript = "window.scrollTo(0, document.body.scrollHeight);";
            heightCheckScript = "return document.body.scrollHeight";
        }

        while (true)
        {
            Driver.ExecuteScript(scrollScript);
            await Task.Delay(scrollPauseTime, cancellationToken);
            var newHeight = Convert.ToInt64(Driver.ExecuteScript(heightCheckScript));
            if (newHeight == lastHeight)
            {
                if (scrollBack > 0)
                {
                    for (var i = 0; i < scrollBack; i++)
                    {
                        Driver.ExecuteScript($"window.scrollBy({{top: {-increment}, left: 0, behavior: 'smooth'}});");
                        await Task.Delay(scrollPauseTime, cancellationToken);
                    }

                    await Task.Delay(scrollPauseTime, cancellationToken);
                }

                break;
            }

            lastHeight = newHeight;
        }
    }

    protected async Task LazyLoad(By elementToFind, int increment = 1250, int scrollPauseTime = 500,
                                  CancellationToken cancellationToken = default)
    {
        var scrollScript = $"window.scrollBy({{top: {increment}, left: 0, behavior: 'smooth'}});";
        while (true)
        {
            Driver.ExecuteScript(scrollScript);
            await Task.Delay(scrollPauseTime, cancellationToken);
            var element = Driver.FindElement(elementToFind);
            if (element.Displayed)
            {
                break;
            }
        }
    }

    protected void ScrollPage(int distance = 1250)
    {
        var currHeight = (long)(Driver.ExecuteScript("return window.pageYOffset") ?? 0);
        var scrollScript = $"window.scrollBy({{top: {currHeight + distance}, left: 0, behavior: 'smooth'}});";
        Driver.ExecuteScript(scrollScript);
    }

    protected void ScrollToTop()
    {
        Driver.ExecuteScript("window.scrollTo(0, 0);");
    }

    protected async Task WaitForPlaylist(PlaylistCapturer capturer, Action<List<string>> callback,
                                         CancellationToken cancellationToken = default)
    {
        var i = 0;
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                i++;
                if (i % 50 == 49)
                {
                    Logger.Debug("No playlist links found yet, refreshing page...");
                    Driver.Refresh();
                }

                await Sleep(250, cancellationToken);
                continue;
            }

            callback(links);
            break;
        }
    }

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

public abstract class HtmlParser<T> : HtmlParser
    where T : HtmlParser<T>, IHtmlParser
{
    protected HtmlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, filenameScheme)
    {
    }
}