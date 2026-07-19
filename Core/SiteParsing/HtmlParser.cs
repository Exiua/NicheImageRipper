using System.Reflection;
using FlareSolverrIntegration.Responses;
using HtmlAgilityPack;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.PartialSaves;
using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium;
using OpenQA.Selenium.BiDi;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Manager;
using Serilog;
using Serilog.Events;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing;

public abstract class HtmlParser : IDisposable
{
    protected const string Protocol = "https:";

    protected static readonly string[] ExternalSites =
        ["drive.google.com", "mega.nz", "mediafire.com", "sendvid.com", "dropbox.com", "youtube.com"];

    protected static GeneralConfig Config => Configuration.Config.Instance;
    protected static TokenManager TokenManager => TokenManager.Instance;
    
    private static PartialSaveManager PartialSaveManager => PartialSaveManager.Instance;

    protected WebDriver WebDriver { get; }
    public bool Interrupted { get; set; }
    private string SiteName { get; set; }
    public float SleepTime { get; set; }
    public float Jitter { get; set; }
    public int RetryCount { get; set; } = 4;
    protected string GivenUrl { get; private set; }
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

    protected static bool Debugging { get; set; }
    protected static FlareSolverrManager FlareSolverrManager => NicheImageRipper.FlareSolverrManager;
    protected static string UserAgent => Config.UserAgent;

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

    public async Task<RipInfo> ParseSite(string url, CancellationToken cancellationToken = default)
    {
        Logger.Debug("Parsing {Url}", url);
        url = url.Replace("members.", "www.") // For HAnime
                 .Replace("exhentai.org", "e-hentai.org"); // Need to go through e-hentai first for cookies
        GivenUrl = url;
        (SiteName, SleepTime) = UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        // e-hentai image links expire too quickly, so we need to parse the site every time
        if ((SiteName != "e-hentai" && SiteName != "exhentai"))
        {
            var saveData = PartialSaveManager.GetPartialSave(url);
            if (saveData is not null)
            {
                Logger.Debug("Partial save found for {Url}", url);
                RequestHeaders["cookie"] = saveData.Cookies;
                RequestHeaders["referer"] = saveData.Referer;
                Interrupted = true;
                return saveData.RipInfo;
            }
        }
        else
        {
            File.Delete(ImageRipper.RipIndexPath); // Not valid when no partial save
        }

        Logger.Debug("No partial save found for site; Parsing site");
        if (SiteName != "booru")
        {
            CurrentUrl = url;
        }

        // Logger.Debug("Getting parser for {SiteName}", SiteName);
        // var siteParser = GetParser(SiteName);
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                Logger.Debug("Executing parser for {SiteName}", SiteName);
                var siteInfo = await Parse(cancellationToken);
                Logger.Debug("Saving partial save for {Url}", url);
                WritePartialSave(siteInfo, url);
                //pickle.dump(self.driver.get_cookies(), open("cookies.pkl", "wb"))
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

    protected HtmlParser GetParser(string url)
    {
        var requestHeaders = new Dictionary<string, string>();
        var (siteName, _) = UrlUtility.SiteCheck(url, requestHeaders);
        var parser = GetParser(siteName, WebDriver, ApiClientManager, requestHeaders, FilenameScheme);
        return parser;
    }

    public static HtmlParser GetParser(string siteName, WebDriver webDriver, ApiClientManager clientManager,
                                       Dictionary<string, string> requestHeaders,
                                       FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        return HtmlParserFactory.Create(siteName, webDriver, clientManager, requestHeaders, filenameScheme);
    }

    private void WritePartialSave(RipInfo ripInfo, string url)
    {
        var partialSaveEntry = new PartialSaveEntry
        {
            Cookies = RequestHeaders["cookie"],
            Referer = RequestHeaders["referer"],
            RipInfo = ripInfo
        };
        PartialSaveManager.AddPartialSave(url, partialSaveEntry);
    }

    // TODO: Make private and call from ParseSite so children only need to implement SiteLoginHelper instead of worrying
    //  about calling SiteLogin as well
    //  Only issue is with GoFileParser/ParameterizedHtmlParser where CurrentUrl may need to be set before login
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

    protected abstract Task<RipInfo> Parse(CancellationToken cancellationToken = default);

    #region Generic Site Parsers

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected async Task<RipInfo> GenericBabesHtmlParser(string dirNameXpath, string imageContainerXpath, CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow(dirNameXpath)
                          .InnerText;
        var images = soup.SelectNodesOrThrow(imageContainerXpath)
                         .SelectMany(im => im.SelectNodesOrThrow(".//img"))
                         .Select(img => Protocol + img.GetSrc().Remove("tn_"))
                         .Select(dummy => (StringFileLinkWrapper)dummy)
                         .ToList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    protected Task<RipInfo> GenericHtmlParser(string siteName, CancellationToken cancellationToken = default)
    {
        return siteName switch
        {
            "bustybloom" or "sexyaporno" => GenericHtmlParserHelper1(cancellationToken),
            "elitebabes" => GenericHtmlParserHelper2(cancellationToken),
            "femjoyhunter" or "ftvhunter" or "hegrehunter" or "joymiihub"
                or "metarthunter" or "pmatehunter" or "xarthunter" => GenericHtmlParserHelper3(cancellationToken),
            _ => throw new RipperException($"Invalid site name: {siteName}")
        };
    }

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericHtmlParserHelper1(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//img[@title='Click To Enlarge!']")
                          .GetAttributeValue("alt")
                          .Split(" ")
                          .TakeWhile(s => s != "-")
                          .Join(" ");
        var images = soup.SelectNodesOrThrow("//div[@class='gallery_thumb']")
                         .Select(img => Protocol + img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("tn_"))
                         .ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericHtmlParserHelper2(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var imageList = soup.SelectSingleNodeOrThrow("//ul[@class='list-gallery static css has-data']")
                            .SelectNodesOrThrow(".//a");
        var images = imageList.Select(image => image.GetHref())
                              .Select(dummy => (StringFileLinkWrapper)dummy)
                              .ToList();
        var dirName = imageList[0].SelectSingleNodeOrThrow(".//img")
                                  .GetAttributeValue("alt");

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericHtmlParserHelper3(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//header[@id='top']").SelectSingleNodeOrThrow(".//h1").InnerText;
        var images = soup
                    .SelectSingleNodeOrThrow(
                         "//ul[contains(@class, 'list-gallery') and contains(@class, 'static') and contains(@class, 'css')]")
                    .SelectNodesOrThrow(".//a")
                    .Select(img => img.GetHref())
                    .Select(dummy => (StringFileLinkWrapper)dummy)
                    .ToList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    #endregion

    protected static string ExtractJsonObject(string json)
    {
        var depth = 0;
        var escaped = false;
        var inString = false;
        foreach (var (i, c) in json.Enumerate())
        {
            if (escaped)
            {
                escaped = false;
            }
            else
            {
                switch (c)
                {
                    case '\\':
                        escaped = true;
                        break;
                    case '{':
                        if (!inString)
                        {
                            depth++;
                        }

                        break;
                    case '}':
                        if (!inString)
                        {
                            depth--;
                        }

                        break;
                    case '"':
                        inString = !inString;
                        break;
                }
            }

            if (depth == 0)
            {
                return json[..(i + 1)];
            }
        }

        throw new RipperException($"Improperly formatted json: {json}");
    }

    /// <summary>
    ///     Convert current page into an HtmlNode object
    /// </summary>
    /// <param name="delay">How long to wait after loading the page (in milliseconds) before parsing</param>
    /// <param name="lazyLoadArgs">Arguments for lazy loading elements on the page</param>
    /// <param name="xpath">XPath of an element to wait for before parsing</param>
    /// <param name="xpathTimout">Timeout (in seconds) for waiting for the XPath element</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>>Parsed HtmlNode object</returns>
    protected async Task<HtmlNode> Soupify(int delay = 0, LazyLoadArgs? lazyLoadArgs = null, string xpath = "",
                                           int xpathTimout = 10, CancellationToken cancellationToken = default)
    {
        if (delay > 0)
        {
            await Task.Delay(delay, cancellationToken);
        }

        if (xpath != "")
        {
            await WaitForElement(xpath, timeout: xpathTimout, cancellationToken: cancellationToken);
        }

        if (lazyLoadArgs is not null)
        {
            await LazyLoad(lazyLoadArgs, cancellationToken);

        }

        var doc = new HtmlDocument();
        doc.LoadHtml(Driver.PageSource);
        return doc.DocumentNode;
    }

    /// <summary>
    ///     Convert input into an HtmlNode object
    /// </summary>
    /// <param name="url">URL or HTML string. If a url is provided, the driver will navigate to it first, before parsing the page.</param>
    /// <param name="delay">How long to wait after loading the page (in milliseconds) before parsing</param>
    /// <param name="lazyLoadArgs">Arguments for lazy loading elements on the page</param>
    /// <param name="xpath">XPath of an element to wait for before parsing</param>
    /// <param name="urlString">Indicates whether the 'url' parameter is a URL (true) or an HTML string (false)</param>
    /// <param name="cookies">Cookies to add before loading the page</param>
    /// <param name="xpathTimout">Timeout (in seconds) for waiting for the XPath element</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>>Parsed HtmlNode object</returns>
    protected async Task<HtmlNode> Soupify(string url, int delay = 0, LazyLoadArgs? lazyLoadArgs = null,
                                           string xpath = "", bool urlString = true, ICookieJar? cookies = null,
                                           int xpathTimout = 10, CancellationToken cancellationToken = default)
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

        return await Soupify(delay: delay, lazyLoadArgs: lazyLoadArgs, xpath: xpath, xpathTimout: xpathTimout, cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     Convert HttpResponseMessage content into an HtmlNode object
    /// </summary>
    /// <param name="response">HttpResponseMessage to parse</param>
    /// <returns>>Parsed HtmlNode object</returns>
    protected static async Task<HtmlNode> Soupify(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(content);
        return htmlDocument.DocumentNode;
    }

    protected async Task<HtmlNode> Soupify(CSWebDriverClient.Models.Responses.BaseResponse baseResponse, CancellationToken cancellationToken = default)
    {
        return baseResponse switch
        {
            CSWebDriverClient.Models.Responses.ErrorResponse errorResponse => 
                errorResponse.Details is not null
                    ? throw new RipperException($"{errorResponse.Error}: {errorResponse.Details}")
                    : throw new RipperException(errorResponse.Error),
            CSWebDriverClient.Models.Responses.PageResponse pageResponse => await Soupify(pageResponse.Content, urlString: false, cancellationToken: cancellationToken),
            CSWebDriverClient.Models.Responses.GetNetworkUrlsResponse =>
                throw new RipperException("Incorrect response type: GetNetworkUrlsResponse"),
            _ => throw new RipperException($"Unknown response type: {baseResponse}")
        };
    }

    /// <summary>
    ///     Convert FlareSolverr Solution response into an HtmlNode object
    /// </summary>
    /// <param name="solution">FlareSolverr Solution to parse</param>
    /// <returns>>Parsed HtmlNode object</returns>
    private static Task<HtmlNode> Soupify(Solution solution, CancellationToken cancellationToken = default)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(solution.Response);
        return Task.FromResult(htmlDocument.DocumentNode);
    }

    /// <summary>
    ///     Wait for an element to exist on the page
    /// </summary>
    /// <param name="xpath">XPath of the element to wait for</param>
    /// <param name="delay">Delay between each check</param>
    /// <param name="timeout">Timeout (in seconds) for the wait (-1 for no timeout)</param>
    /// <returns>True if the element exists, false if the timeout is reached</returns>
    protected async Task<string?> WaitForElement(string xpath, float delay = 0.1f, float timeout = 10, CancellationToken cancellationToken = default)
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

    /// <summary>
    ///     Close all tabs that do not contain the specified URL match.
    /// </summary>
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

    /// <summary>
    ///     Solves a CAPTCHA using FlareSolverr, parses the returned HTML, and adds the necessary cookies to the browser session.
    /// </summary>
    /// <param name="regenerateSessionOnFailure">
    ///     If <c>true</c>, regenerates the session and retries once if CAPTCHA solving fails.
    /// </param>
    /// <param name="cookies">
    ///     Optional. A list of cookie dictionaries to include in the session when solving the CAPTCHA.
    /// </param>
    /// <param name="cookieWhitelist">List of cookie names to retain from the existing session.</param>
    /// <param name="replaceUserAgent">Whether to replace the User-Agent header with the one provided by FlareSolverr.</param>
    /// <returns>
    ///     The parsed HTML document as an <see cref="HtmlNode"/>.
    /// </returns>
    /// <exception cref="FeatureNotAvailableException">
    ///     Thrown if FlareSolverr support is not available.
    /// </exception>
    /// <exception cref="FailedToGetSolutionException">
    ///     Thrown if CAPTCHA solving fails and session regeneration is disabled.
    /// </exception>
    protected async Task<HtmlNode> SolveParseAddCookies(bool regenerateSessionOnFailure = false,
                                                        List<Dictionary<string, string>>? cookies = null,
                                                        List<string>? cookieWhitelist = null,
                                                        bool replaceUserAgent = false, CancellationToken cancellationToken = default)
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

    protected async Task<HtmlNode> SolveParse(bool regenerateSessionOnFailure = false,
                                              List<Dictionary<string, string>>? cookies = null, CancellationToken cancellationToken = default)
    {
        var solution = await Solve(regenerateSessionOnFailure, cookies, cancellationToken);
        return await Soupify(solution, cancellationToken: cancellationToken);
    }

    private async Task<Solution> Solve(bool regenerateSessionOnFailure = false,
                                       List<Dictionary<string, string>>? cookies = null, CancellationToken cancellationToken = default)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.FlareSolverr))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.FlareSolverr);
        }

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

    internal static Task JitterSleep(int min = 250, int max = 2500, CancellationToken cancellationToken = default)
    {
        var jitter = Random.Shared.Next(min, max);
        return Task.Delay(jitter, cancellationToken);
    }

    protected static Task Sleep(int milliseconds, CancellationToken cancellationToken = default)
    {
        return Task.Delay(milliseconds, cancellationToken);
    }

    protected static async Task<T> RetryUntil<T>(Func<Task<T>> func, Func<T, bool> successCondition,
                                                 string errorMessage, int delay = 250, CancellationToken cancellationToken = default)
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

    protected static async Task<T> DeserializeCache<T>(string cachePath, Func<Task<T>> fetchFunc, CancellationToken cancellationToken = default) where T : class
    {
        T data;
        if (File.Exists(cachePath))
        {
            var temp = JsonUtility.Deserialize<T>(cachePath);
            if (temp is null)
            {
                data = await fetchFunc();
            }
            else
            {
                data = temp;
            }
        }
        else
        {
            data = await fetchFunc();
        }

        return data;
    }

    protected async Task<(T, IBiDi)> ConfigureNetworkCapture<T>(CancellationToken cancellationToken = default) where T : PlaylistCapturer, new()
    {
        var capturer = new T();
        var bidi = await Driver.AsBiDiAsync(cancellationToken: cancellationToken);
        await bidi.Network.OnResponseCompletedAsync(capturer.CaptureHook, cancellationToken: cancellationToken);
        return (capturer, bidi);
    }

    protected static Dictionary<string, List<string>> CreateExternalLinkDict()
    {
        var externalLinks = new Dictionary<string, List<string>>();
        foreach (var site in ExternalSites)
        {
            externalLinks[site] = [];
        }

        return externalLinks;
    }

    protected static Dictionary<string, List<string>> ExtractExternalUrls(IEnumerable<string> urls)
    {
        var externalLinks = CreateExternalLinkDict();
        var urlList = urls.ToList();
        foreach (var site in externalLinks.Keys)
        {
            foreach (var link in urlList.Where(url => !string.IsNullOrEmpty(url) && url.Contains(site))
                                        .Select(UrlUtility.ExtractUrl)
                                        .Where(link => link != ""))
            {
                externalLinks[site].Add(link + '\n');
            }
        }

        return externalLinks;
    }

    protected static void SaveExternalLinks(Dictionary<string, List<string>> links)
    {
        foreach (var (site, siteLinks) in links)
        {
            if (siteLinks.Count == 0)
            {
                continue;
            }

            File.AppendAllLines($"{site}_links.txt", siteLinks);
        }
    }

    protected static bool UrlCanBeParsed(string url)
    {
        return !string.IsNullOrEmpty(url) && ExternalSites.Any(url.Contains);
    }

    /// <summary>
    ///     Scrolls through the page to lazy load images
    /// </summary>
    /// <param name="args">Arguments for lazy loading</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    protected Task LazyLoad(LazyLoadArgs args, CancellationToken cancellationToken = default)
    {
        return args.StopElement is not null && Driver.TryFindElement(args.StopElement) is not null
            ? LazyLoad(args.StopElement, cancellationToken: cancellationToken)
            : LazyLoad(args.ScrollBy, args.Increment, args.ScrollPauseTime, args.ScrollBack, args.ReScroll, cancellationToken);
    }

    /// <summary>
    ///     Scroll through the page to lazy load images
    /// </summary>
    /// <param name="scrollBy">Whether to scroll through the page or instantly scroll to the bottom</param>
    /// <param name="increment">Distance to scroll by each iteration</param>
    /// <param name="scrollPauseTime">Seconds to wait between each scroll</param>
    /// <param name="scrollBack">Distance to scroll back by after reaching the bottom of the page</param>
    /// <param name="rescroll">Whether scrolling through the page again</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    protected async Task LazyLoad(bool scrollBy = false, int increment = 2500, int scrollPauseTime = 500,
                                  int scrollBack = 0, bool rescroll = false, CancellationToken cancellationToken = default)
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

    protected async Task LazyLoad(By elementToFind, int increment = 1250, int scrollPauseTime = 500, CancellationToken cancellationToken = default)
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

    protected async Task WaitForPlaylist(PlaylistCapturer capturer, Action<List<string>> callback, CancellationToken cancellationToken = default)
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

    protected static void LogFailedUrl(string url)
    {
        File.AppendAllText("failed.txt", $"{url}\n");
    }

    #region Parser Testing

    public async Task<RipInfo> TestParse(string givenUrl, bool debug, bool printSite, CancellationToken cancellationToken = default)
    {
        try
        {
            OpenQA.Selenium.Internal.Logging.Log.SetLevel(
                typeof(OpenQA.Selenium.Remote.RemoteWebDriver),
                OpenQA.Selenium.Internal.Logging.LogEventLevel.Trace);
            OpenQA.Selenium.Internal.Logging.Log.SetLevel(
                typeof(SeleniumManager),
                OpenQA.Selenium.Internal.Logging.LogEventLevel.Trace);
            /*var options = InitializeOptions(debug);
            Driver = new FirefoxDriver(options);*/
            CurrentUrl = givenUrl.Replace("members.", "www.");
            SiteName = TestSiteCheck(givenUrl);

            Logger.Debug("Testing: {SiteName}Parse", SiteName);
            Logger.Debug("URL: {CurrentUrl}", CurrentUrl);
            var start = DateTime.Now;
            var data = await EvaluateParser(SiteName, cancellationToken);
            var end = DateTime.Now;
            if (data.Urls.Count == 0)
            {
                Logger.Error("No URLs found for {SiteName}Parse", SiteName);
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
            Logger.Error(e, "Error occurred while testing {SiteName}Parse", SiteName);
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

            //await FlareSolverrManager.DeleteSession();
        }
    }

    private Task<RipInfo> EvaluateParser(string siteName, CancellationToken cancellationToken = default)
    {
        siteName = TestSiteConverter(siteName);
        siteName = siteName[0].ToString().ToUpper() + siteName[1..];
        var className = $"{siteName}Parser";
        Logger.Debug("Parser: {ParserName}", className);
        var classType = Assembly.GetExecutingAssembly()
                                .GetTypes()
                                .FirstOrDefault(t =>
                                     string.Equals(t.Name, className, StringComparison.OrdinalIgnoreCase));
        if (classType is not null)
        {
            var ripper =
                (HtmlParser)Activator.CreateInstance(classType, WebDriver, ApiClientManager, RequestHeaders,
                    FilenameScheme)!;
            return ripper.Parse(cancellationToken);
        }

        // Handle the case where the method does not exist
        Logger.Error("Parser {ParserName} not found.", className);
        throw new InvalidOperationException();
    }

    private static string TestSiteConverter(string siteName)
    {
        if (siteName == "x")
        {
            return "twitter";
        }

        if (siteName == "booru")
        {
            return "allbooru";
        }

        if (siteName == "x-x-x")
        {
            return "xxxtube";
        }

        if (siteName.Contains("bunkrrr"))
        {
            siteName = siteName.Replace("bunkrrr", "Bunkr");
        }
        else if (siteName.Contains("100bucksbabes"))
        {
            siteName = siteName.Replace("100bucksbabes", "HundredBucksBabes");
        }
        else if (siteName.Contains("chapmanganato"))
        {
            siteName = siteName.Replace("chapmanganato", "Manganato");
        }
        else if (siteName.Contains("18kami"))
        {
            siteName = siteName.Replace("18kami", "EighteenKami");
        }

        if (siteName[0] >= '0' && siteName[0] <= '9')
        {
            siteName = NumberToWord(siteName[0]) + char.ToUpper(siteName[1]) + siteName[2..];
        }

        return siteName.Remove("-");
    }

    private static string NumberToWord(char number)
    {
        return number switch
        {
            '0' => "zero",
            '1' => "one",
            '2' => "two",
            '3' => "three",
            '4' => "four",
            '5' => "five",
            '6' => "six",
            '7' => "seven",
            '8' => "eight",
            '9' => "nine",
            _ => throw new RipperException("Invalid number")
        };
    }

    private string TestSiteCheck(string url)
    {
        var domain = new Uri(url).Host;
        RequestHeaders["referer"] = $"https://{domain}/";
        domain = DomainNameOverride(domain);
        if (url.Contains("https://members.hanime.tv/") || url.Contains("https://hanime.tv/"))
        {
            RequestHeaders["referer"] = "https://cdn.discordapp.com/";
        }
        else if (url.Contains("https://kemono.party/"))
        {
            RequestHeaders["referer"] = "";
        }

        return domain;
    }

    private static string DomainNameOverride(string url)
    {
        string[] specialDomains = ["inven.co.kr", "danbooru.donmai.us"];
        var urlSplit = url.Split(".");
        return specialDomains.Any(url.Contains) ? urlSplit[^3] : urlSplit[^2];
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

public abstract class HtmlParser<T> : HtmlParser
    where T : HtmlParser<T>, IHtmlParser
{
    protected HtmlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, filenameScheme)
    {
    }
}