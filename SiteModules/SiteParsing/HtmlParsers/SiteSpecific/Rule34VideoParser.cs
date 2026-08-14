using CSWebDriverClient;
using CSWebDriverClient.Models.Responses;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using OpenQA.Selenium;
using HtmlAgilityPack;
using NicheImageRipper.Core.Utility;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;
using ErrorResponse = CSWebDriverClient.Models.Responses.ErrorResponse;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class Rule34VideoParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "rule34video";
    public static string[] SupportedUrls => ["https://rule34video.com/"];

    public Rule34VideoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Rule34VideoParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for rule34video.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        if (!Core.NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.CSWebDriver))
        {
            Logger.Error("CSWebDriver URI is not configured. Cannot parse rule34video.com without CSWebDriver.");
            throw new FeatureNotAvailableException(ExternalFeatureSupport.CSWebDriver);
        }

        var client = new Client(Config.CSWebDriverUri);
        await Sleep(500, cancellationToken);
        Logger.Debug("Searching for continue button");
        var continueButton = Driver.TryFindElement(By.XPath("//input[@name='continue']"));
        if (continueButton is not null)
        {
            Logger.Debug("Clicking continue button");
            try
            {
                continueButton.Click();
            }
            catch (ElementNotInteractableException)
            {
                // Usually occurs when cookies from the previous session are still present
                Logger.Debug("Popup probably not active");
            }
        }
        else
        {
            Logger.Debug("Continue button not found");
        }

        var requestCookies = new Dictionary<string, string>
        {
            ["kt_rt_popAccess"] = "1"
        };
        var response = await client.GetPage(CurrentUrl, cookies: requestCookies, cancellationToken: cancellationToken);
        if (response is ErrorResponse errorResponse)
        {
            Logger.Error("Error retrieving page: {Error}", errorResponse.Error);
            throw new RipperException("Error retrieving page: " + errorResponse.Error);
        }

        var pageResponse = (PageResponse)response;
        var soup = await Soupify(pageResponse.Content, urlString: false, cancellationToken: cancellationToken);
        string dirName;
        List<StringFileLinkWrapper> files;
        if (CurrentUrl.Contains("/models/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//div[@class='title']").InnerText;
            files = await GetVideoForModel(client, soup);
        }
        else if (CurrentUrl.Contains("/video/"))
        {
            files = [];
            (var downloadLink, dirName) = await GetVideoDownloadUrl(soup: soup, cancellationToken: cancellationToken);
            var fileLink = FileLink.WithFilename(downloadLink, $"{dirName}.mp4", FilenameScheme);
            files.Add(fileLink);
        }
        else if (CurrentUrl.Contains("/search/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//h1[@class='title']").ChildNodes[0].InnerText;
            files = await GetVideoForSearch(client, soup);
        }
        else
        {
            throw new RipperException("Unknown page type: " + CurrentUrl);
        }

        var cookieJar = Driver.GetCookieJar();
        var cookies = cookieJar.AllCookies.GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.First().Value);
        var cookieString = cookies.Select(kvp => $"{kvp.Key}={kvp.Value}").Join("; ");
        RequestHeaders[RequestHeaderKeys.Cookie] = cookieString;
        return RipInfo.FromUrlList(files, dirName, FilenameScheme);
    }

    private Task<List<StringFileLinkWrapper>> GetVideoForSearch(Client client, HtmlNode soup)
    {
        return GetVideoForPage(client, soup, "//div[@id='custom_list_videos_videos_list_search_items']");
    }

    private Task<List<StringFileLinkWrapper>> GetVideoForModel(Client client, HtmlNode soup)
    {
        return GetVideoForPage(client, soup, "//div[@id='custom_list_videos_common_videos_items']");
    }

    private async Task<List<StringFileLinkWrapper>> GetVideoForPage(Client client, HtmlNode soup, string videoXpath)
    {
        var videoPosts = new List<string>();
        var page = 1;
        while (true)
        {
            Logger.Information("Parsing page {Page}", page++);
            var videos = soup.SelectSingleNodeOrThrow(videoXpath).SelectNodesOrThrow("./div").Select(div => div.SelectSingleNode("./a[@class='th js-open-popup']")?.GetHref().DecodeUrl()).Where(s => s is not null);
            videoPosts.AddRange(videos!);
            Logger.Debug("Checking if there is a blockOverlay");
            var uiBlock = Driver.TryFindElement(By.XPath("//div[@class='blockUI blockOverlay']"));
            while (uiBlock is not null)
            {
                Logger.Debug("Waiting for blockOverlay to disappear");
                await Sleep(500);
                uiBlock = Driver.TryFindElement(By.XPath("//div[@class='blockUI blockOverlay']"));
            }

            Logger.Debug("Searching for next button");
            var nextButtonResponse = await client.PressButtonOnPage("//div[@class='item pager next']/a");
            if (nextButtonResponse is ErrorResponse nextButtonError)
            {
                Logger.Error("Error fetching next page: {Error}", nextButtonError.Error);
                break;
            }

            var nextPageResponse = (PageResponse)nextButtonResponse;
            if (nextPageResponse.Content == "")
            {
                Logger.Debug("No more pages found");
                break;
            }

            soup = await Soupify(nextPageResponse.Content, urlString: false);
        }

        return await ParseVideoPosts(videoPosts);
    }

    private async Task<List<StringFileLinkWrapper>> ParseVideoPosts(List<string> videoPosts)
    {
        var files = new List<StringFileLinkWrapper>();
        foreach (var post in videoPosts)
        {
            var(downloadLink, title) = await GetVideoDownloadUrl(post: post);
            var fileLink = FileLink.WithFilename(downloadLink, $"{title}.mp4", FilenameScheme);
            files.Add(fileLink);
        }

        return files;
    }

    private async Task<(string, string)> GetVideoDownloadUrl(HtmlNode? soup = null, string? post = null, CancellationToken cancellationToken = default)
    {
        if (soup is null)
        {
            if (post is null)
            {
                Logger.Error("Either soup or post must be provided");
                throw new ArgumentException("Either soup or post must be provided");
            }

            Logger.Debug("Parsing post: {Post}", post);
            CurrentUrl = post;
            soup = await SolveParse(cancellationToken: cancellationToken);
        }

        var title = soup.SelectSingleNodeOrThrow("//h1[@class='title_video']").InnerText;
        Logger.Debug("Searching for video info");
        var videoInfo = soup.SelectSingleNodeOrThrow("//div[@id='tab_video_info']");
        Logger.Debug("Searching for downloads");
        var downloads = videoInfo.SelectNodesOrThrow("./div")[^1];
        Logger.Debug("Grabbing download link");
        // First link is the highest quality
        var downloadLink = downloads.SelectSingleNodeOrThrow(".//a").GetHref().DecodeUrl();
        return (downloadLink, title);
    }
}