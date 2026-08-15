using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using OpenQA.Selenium;
using FeatureNotSupportedException = NicheImageRipper.Core.Exceptions.NotSupportedException;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.PmvHaven;

public class PmvHavenParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "pmvhaven";
    public static string[] SupportedUrls => ["https://pmvhaven.com/"];

    public PmvHavenParser(WebDriver driver, ApiClientManager apiClientManager,
                          Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<PmvHavenParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for pmvhaven.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        //var client = new CSWebDriverClient.Client(Config.CSWebDriverUri);
        var (capturer, b) = await ConfigureNetworkCapture<PmvHavenCapturer>(cancellationToken);
        await using var bidi = b;
        Driver.AddCookie("ageVerified", "true");
        Driver.Refresh();
        var button = Driver.TryFindElement(By.XPath("//button[@class='btn-confirm']"));
        if (button is not null)
        {
            button.Click();
            await Sleep(250, cancellationToken);
        }

        var currentUrl = CurrentUrl;
        string dirName;
        List<StringFileLinkWrapper> images;
        if (currentUrl.Contains("/video/"))
        {
            var soup = await Soupify(xpath: "//video[@id='VideoPlayer']/source", xpathTimeout: 10,
                cancellationToken: cancellationToken);
            dirName = soup.SelectNodesOrThrow("//h1")[1].InnerText;
            var url = await GetVideoUrl(capturer, currentUrl, cancellationToken);
            images = [url];
        }
        else if (currentUrl.Contains("/profile/"))
        {
            var soup = await Soupify(cancellationToken: cancellationToken);
            dirName = soup.SelectNodesOrThrow("//h1")[1].InnerText;
            List<string> videoPosts = [];
            while (true)
            {
                var videoGrid = soup.SelectSingleNodeOrThrow("//div[@class='videos-grid-fixed']");
                var posts = videoGrid.SelectNodesOrThrow("./a").Select(a => "https://pmvhaven.com" + a.GetHref());
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
                soup = await Soupify(cancellationToken: cancellationToken);
            }

            Logger.Information("Found {count} videos in profile.", videoPosts.Count);
            images = [];
            foreach (var post in videoPosts)
            {
                Logger.Information("Parsing video post: {post}", post);
                CurrentUrl = post;
                var url = await GetVideoUrl(capturer, post, cancellationToken);
                images.Add(url);
                await Sleep(250, cancellationToken);
            }
        }
        else
        {
            throw new FeatureNotSupportedException(CurrentUrl, "Only videos and profiles are supported for parsing.");
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<FileLink> GetVideoUrl(PmvHavenCapturer capturer, string postUrl,
                                             CancellationToken cancellationToken = default)
    {
        postUrl = postUrl.Split("?")[0];
        FileLink? imageLink = null;
        await WaitForPlaylist(capturer, links =>
        {
            var url = links[0];
            var filename = postUrl.Split('/')[4] + ".mp4";
            var link = FileLink.WithFilename(url, filename, FilenameScheme, linkInfo: LinkInfo.M3U8Ffmpeg);
            imageLink = link;
        }, cancellationToken);
        if (imageLink is null)
        {
            throw new RipperException("Failed to retrieve video URL from network capturer.");
        }

        return imageLink;
    }
}