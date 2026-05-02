using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.SiteParsing.VideoCapturers;
using OpenQA.Selenium;
using Serilog;
using FeatureNotSupportedException = Core.Exceptions.NotSupportedException;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PmvHavenParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "pmvhaven";

    public PmvHavenParser(WebDriver driver, ApiClientManager apiClientManager,
                          Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<PmvHavenParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for pmvhaven.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        //var client = new CSWebDriverClient.Client(Config.CSWebDriverUri);
        var (capturer, b) = await ConfigureNetworkCapture<PmvHavenCapturer>();
        await using var bidi = b;
        Driver.AddCookie("ageVerified", "true");
        Driver.Refresh();
        var button = Driver.TryFindElement(By.XPath("//button[@class='btn-confirm']"));
        if (button is not null)
        {
            button.Click();
            await Sleep(250);
        }
        
        var currentUrl = CurrentUrl;
        string dirName;
        List<StringImageLinkWrapper> images;
        if (currentUrl.Contains("/video/"))
        {
            var soup = await Soupify(xpath: "//video[@id='VideoPlayer']/source", xpathTimout: 10);
            dirName = soup.SelectNodesOrThrow("//h1")[1].InnerText;
            var url = await GetVideoUrl(capturer, currentUrl);
            images = [url];
        }
        else if(currentUrl.Contains("/profile/"))
        {
            // var cookies = new Dictionary<string, string>
            // {
            //     ["ageVerified"] = "true"
            // };
            // await client.GetPage(CurrentUrl, cookies: cookies);
            // await client.PressButtonOnPage("//button[@class='btn-confirm']");
            var soup = await Soupify();
            dirName = soup.SelectNodesOrThrow("//h1")[1].InnerText;
            List<string> videoPosts = [];
            while (true)
            {
                var videoGrid = soup.SelectSingleNodeOrThrow("//div[@class='videos-grid-fixed']");
                var posts = videoGrid.SelectNodesOrThrow("./a")
                                          .Select(a => "https://pmvhaven.com" + a.GetHref());
                videoPosts.AddRange(posts);
                var navButtons = soup.SelectNodes("//nav/button");
                if (navButtons is null || navButtons.Count == 0)
                {
                    break;
                }

                var nextButton = navButtons.Last();
                var ariaLabel = nextButton.GetAttributeValue("aria-label");
                if (ariaLabel != "Next page")
                {
                    break;
                }
                
                var disabled = nextButton.GetAttributeValue("disabled", "null");
                if (disabled != "null") // if disabled attribute exists, it won't have a value
                {
                    break;
                }

                var nextButtonElement = Driver.TryFindElement(By.XPath("//nav/button[last()]"));
                if (nextButtonElement is null)
                {
                    throw new RipperException("Failed to find next page button on the page.");
                }
                
                nextButtonElement.Click();
                soup = await Soupify();
                // var response = await client.PressButtonOnPage("//nav/button[last()]");
                // if (response is ErrorResponse errorResponse)
                // {
                //     throw new RipperException("Failed to navigate to next page. Reason: " + errorResponse.Error);
                // }
                //
                // var pageResponse = (PageResponse)response;
                // soup = await Soupify(pageResponse.Content, urlString: false);
            }
            
            Logger.Information("Found {count} videos in profile.", videoPosts.Count);
            images = [];
            foreach (var post in videoPosts)
            {
                Logger.Information("Parsing video post: {post}", post);
                CurrentUrl = post;
                var url = await GetVideoUrl(capturer, post);
                images.Add(url);
                await Sleep(250);
            }
        }
        else
        {
            throw new FeatureNotSupportedException(CurrentUrl, "Only videos and profiles are supported for parsing.");
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<ImageLink> GetVideoUrl(PmvHavenCapturer capturer, string postUrl)
    {
        postUrl = postUrl.Split("?")[0];
        ImageLink? imageLink = null;
        await WaitForPlaylist(capturer, links =>
        {
            var url = links[0];
            var filename = postUrl.Split('/')[4] + ".mp4";
            var link = new ImageLink(url, FilenameScheme, 0, filename: filename)
            {
                LinkInfo = LinkInfo.M3U8Ffmpeg,
            };
            
            imageLink = link;
        });
        
        if (imageLink is null)
        {
            throw new RipperException("Failed to retrieve video URL from network capturer.");
        }
        
        return imageLink;
    }
}