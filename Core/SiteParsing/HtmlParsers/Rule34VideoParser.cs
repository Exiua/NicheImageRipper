using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.Utility;
using CSWebDriverClient;
using CSWebDriverClient.Models.Responses;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;
using ErrorResponse = CSWebDriverClient.Models.Responses.ErrorResponse;

namespace Core.SiteParsing.HtmlParsers;

public class Rule34VideoParser : HtmlParser
{
    public Rule34VideoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for rule34video.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.CSWebDriver))
        {
            Log.Error("CSWebDriver URI is not configured. Cannot parse rule34video.com without CSWebDriver.");
            throw new FeatureNotAvailableException(ExternalFeatureSupport.CSWebDriver);
        }
        
        var client = new Client(Config.CSWebDriverUri);
        await Sleep(500);
        Log.Debug("Searching for continue button");
        var continueButton = Driver.TryFindElement(By.XPath("//input[@name='continue']"));
        if (continueButton is not null)
        {
            Log.Debug("Clicking continue button");
            try
            {
                continueButton.Click();
            }
            catch (ElementNotInteractableException)
            {
                // Usually occurs when cookies from the previous session are still present
                Log.Debug("Popup probably not active");
            }
        }
        else
        {
            Log.Debug("Continue button not found");
        }


        var requestCookies = new Dictionary<string, string>
        {
            ["kt_rt_popAccess"] = "1"
        };
        var response = await client.GetPage(CurrentUrl, cookies: requestCookies);
        if (response is ErrorResponse errorResponse)
        {
            Log.Error("Error retrieving page: {Error}", errorResponse.Error);
            throw new RipperException("Error retrieving page: " + errorResponse.Error);
        }

        var pageResponse = (PageResponse)response;
        var soup = await Soupify(pageResponse.Content, urlString: false);
        string dirName;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/models/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//div[@class='title']").InnerText;
            images = await GetVideoForModel(client, soup);
        }
        else if (CurrentUrl.Contains("/video/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//h1[@class='title_video']").InnerText;
            images = [];
            var downloadLink = await GetVideoDownloadUrl(soup: soup);
            images.Add(downloadLink);
        }
        else if (CurrentUrl.Contains("/search/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//h1[@class='title']").ChildNodes[0].InnerText;
            images = await GetVideoForSearch(client, soup);
        }
        else
        {
            throw new RipperException("Unknown page type: " + CurrentUrl);
        }

        var cookieJar = Driver.GetCookieJar();
        var cookies = cookieJar.AllCookies
                               .GroupBy(c => c.Name)
                               .ToDictionary(g => g.Key, g => g.First().Value);
        var cookieString = cookies.Select(kvp => $"{kvp.Key}={kvp.Value}")
                                  .Join("; ");
        RequestHeaders[RequestHeaderKeys.Cookie] = cookieString;
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private Task<List<StringImageLinkWrapper>> GetVideoForSearch(Client client, HtmlNode soup)
    {
        return GetVideoForPage(client, soup, "//div[@id='custom_list_videos_videos_list_search_items']");
    }

    private Task<List<StringImageLinkWrapper>> GetVideoForModel(Client client, HtmlNode soup)
    {
        return GetVideoForPage(client, soup, "//div[@id='custom_list_videos_common_videos_items']");
    }
    
    private async Task<List<StringImageLinkWrapper>> GetVideoForPage(Client client, HtmlNode soup, string videoXpath)
    {
        var videoPosts = new List<string>();
        var page = 1;
        while (true)
        {
            Log.Information("Parsing page {Page}", page++);
            var videos = soup.SelectSingleNodeOrThrow(videoXpath)
                             .SelectNodesOrThrow("./div")
                             .Select(div => div.SelectSingleNode("./a[@class='th js-open-popup']")?
                                               .GetHref()
                                               .DecodeUrl())
                             .Where(s => s is not null);
            videoPosts.AddRange(videos!);
            
            Log.Debug("Checking if there is a blockOverlay");
            var uiBlock = Driver.TryFindElement(By.XPath("//div[@class='blockUI blockOverlay']"));
            while (uiBlock is not null)
            {
                Log.Debug("Waiting for blockOverlay to disappear");
                await Sleep(500);
                uiBlock = Driver.TryFindElement(By.XPath("//div[@class='blockUI blockOverlay']"));
            }
            
            Log.Debug("Searching for next button");
            var nextButtonResponse = await client.PressButtonOnPage("//div[@class='item pager next']/a");
            if (nextButtonResponse is ErrorResponse nextButtonError)
            {
                Log.Error("Error fetching next page: {Error}", nextButtonError.Error);
                break;
            }
            
            var nextPageResponse = (PageResponse)nextButtonResponse;
            if (nextPageResponse.Content == "")
            {
                Log.Debug("No more pages found");
                break;
            }
            
            soup = await Soupify(nextPageResponse.Content, urlString: false);
            // var nextButton = Driver.TryFindElement(By.XPath("//div[@class='item pager next']/a"));
            // if (nextButton is null)
            // {
            //     Log.Debug("Next button not found");
            //     break;
            // }
            //
            // Log.Debug("Clicking next button");  
            // nextButton.Click();
            // soup = await Soupify(delay: 500);
        }

        return await ParseVideoPosts(videoPosts);
    }

    private async Task<List<StringImageLinkWrapper>> ParseVideoPosts(List<string> videoPosts)
    {
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in videoPosts)
        {
            var downloadLink = await GetVideoDownloadUrl(post: post);
            images.Add(downloadLink);
        }
        
        return images;
    }

    private async Task<string> GetVideoDownloadUrl(HtmlNode? soup = null, string? post = null)
    {
        if (soup is null)
        {
            if (post is null)
            {
                Log.Error("Either soup or post must be provided");
                throw new ArgumentException("Either soup or post must be provided");
            }
            
            Log.Debug("Parsing post: {Post}", post);
            CurrentUrl = post;
            soup = await SolveParse();
        }
        
        Log.Debug("Searching for video info");
        var videoInfo = soup.SelectSingleNodeOrThrow("//div[@id='tab_video_info']");
        Log.Debug("Searching for downloads");
        var downloads = videoInfo.SelectNodesOrThrow("./div")[^1];
        Log.Debug("Grabbing download link");
        // First link is the highest quality
        var downloadLink = downloads.SelectSingleNodeOrThrow(".//a").GetHref().DecodeUrl();
        return downloadLink;
    }
}
