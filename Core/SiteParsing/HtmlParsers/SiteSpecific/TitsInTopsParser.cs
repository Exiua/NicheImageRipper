using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class TitsInTopsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "titsintops";

    private const string SiteUrl = "https://titsintops.com";

    protected override bool RequiresLogin => true;

    public TitsInTopsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                            FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<TitsInTopsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for titsintops.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var cookies = Driver.GetCookieJar();
        var cookieStr =
            cookies.AllCookies.Aggregate("", (current, cookie) => current + $"{cookie.Name}={cookie.Value};");
        RequestHeaders["cookie"] = cookieStr;
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='p-title-value']")
                          .InnerText;
        var images = new List<StringFileLinkWrapper>();
        var externalLinks = CreateExternalLinkDict();
        var pageCount = 1;
        while (true)
        {
            Logger.Information("Parsing page {PageCount}", pageCount);
            pageCount++;
            var posts = soup.SelectSingleNodeOrThrow("//div[@class='block-body js-replyNewMessageContainer']")
                            .SelectNodesOrThrow(".//div[@class='message-content js-messageContent']");
            foreach (var post in posts)
            {
                var imgs = post.SelectSingleNodeOrThrow(".//article[@class='message-body js-selectToQuote']")
                               .SelectNodes(".//img");
                if (imgs is not null)
                {
                    var imgList = imgs.Select(im => im.GetSrc())
                                      .Where(im => im.Contains("http"));
                    images.AddRange(imgList.Select(im => (StringFileLinkWrapper)im));
                }

                var videos = post.SelectNodes(".//video");
                if (videos is not null)
                {
                    var videoUrls = videos.Select(vid =>
                        $"https://titsintops.com{vid.SelectSingleNodeOrThrow(".//source").GetSrc()}");
                    images.AddRange(videoUrls.Select(vid => (StringFileLinkWrapper)vid));
                }

                var iframes = post.SelectNodes(".//iframe");
                if (iframes is not null)
                {
                    var embeddedUrls = iframes.Select(em => em.GetSrc())
                                              .Where(em => em.Contains("http"));
                    embeddedUrls = await ParseEmbeddedUrls(embeddedUrls, cancellationToken);
                    images.AddRange(embeddedUrls.Select(em => (StringFileLinkWrapper)em));
                }

                var attachments = post.SelectSingleNode(".//ul[@class='attachmentList']");
                var attachments2 = attachments?.SelectNodes(".//a[@class='file-preview js-lbImage']");
                if (attachments2 != null)
                {
                    var attachList = attachments2.Select(attach => $"https://titsintops.com{attach.GetHref()}");
                    images.AddRange(attachList.Select(attach => (StringFileLinkWrapper)attach));
                }

                var links = post.SelectSingleNodeOrThrow(".//article[@class='message-body js-selectToQuote']")
                                .SelectNodes(".//a");
                if (links is not null)
                {
                    var linkList = links.Select(link => link.GetNullableHref())
                                        .Where(link => link is not null);
                    var filteredLinks = ExtractExternalUrls(linkList!);
                    var downloadableLinks =
                        await ExtractDownloadableLinks(filteredLinks, externalLinks, cancellationToken);
                    images.AddRange(downloadableLinks.Select(link => (StringFileLinkWrapper)link));
                }
            }

            var nextPage = soup.SelectSingleNode("//a[@class='pageNav-jump pageNav-jump--next']");
            if (nextPage is null)
            {
                SaveExternalLinks(externalLinks);
                break;
            }

            var nextPageUrl = nextPage.GetHref();
            soup = await Soupify($"{SiteUrl}{nextPageUrl}", cancellationToken: cancellationToken);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    protected override async Task<bool> SiteLoginHelper(CancellationToken cancellationToken = default)
    {
        var origUrl = CurrentUrl;
        var (username, password) = Config.Logins.TitsInTops;
        CurrentUrl = "https://titsintops.com/phpBB2/index.php?login/login";
        var loginInput = Driver.TryFindElement(By.XPath("//input[@name='login']"));
        while (loginInput is null)
        {
            await Sleep(100, cancellationToken);
            loginInput = Driver.TryFindElement(By.XPath("//input[@name='login']"));
        }

        loginInput.SendKeys(username);
        var passwordInput = Driver.FindElement(By.XPath("//input[@name='password']"));
        passwordInput.SendKeys(password);
        Driver.FindElement(By.XPath("//button[@class='button--primary button button--icon button--icon--login']"))
              .Click();
        while (Driver.TryFindElement(
                   By.XPath("//button[@class='button--primary button button--icon button--icon--login']")) is not null)
        {
            await Sleep(100, cancellationToken);
        }

        CurrentUrl = origUrl;
        return true;
    }

    private Task<List<string>> ExtractDownloadableLinks(Dictionary<string, List<string>> srcDict,
                                                        Dictionary<string, List<string>> dstDict,
                                                        CancellationToken cancellationToken = default)
    {
        var downloadableLinks = new List<string>();
        var downloadableSites = new[] { "sendvid.com" };
        foreach (var site in srcDict.Keys)
        {
            if (downloadableSites.Contains(site))
            {
                downloadableLinks.AddRange(srcDict[site]);
                srcDict[site].Clear();
            }
            else
            {
                dstDict[site].AddRange(srcDict[site]);
            }
        }

        return ResolveDownloadableLinks(downloadableLinks, cancellationToken);
    }

    private async Task<List<string>> ResolveDownloadableLinks(List<string> links,
                                                              CancellationToken cancellationToken = default)
    {
        var resolvedLinks = new List<string>();
        foreach (var link in links)
        {
            if (link.Contains("sendvid.com"))
            {
                var response = await HttpClient.GetAsync(link, cancellationToken);
                var soup = await Soupify(response, cancellationToken);
                var sourceLink = soup.SelectSingleNodeOrThrow("//source[@id='video_source']")
                                     .GetAttributeValue("src", "");
                resolvedLinks.Add(sourceLink);
            }
            else
            {
                resolvedLinks.Add(link);
            }
        }

        return resolvedLinks;
    }

    private async Task<List<string>> ParseEmbeddedUrls(IEnumerable<string> urls,
                                                       CancellationToken cancellationToken = default)
    {
        var parsedUrls = new List<string>();
        var imgurKey = Config.Keys.Imgur;
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Client-Id {imgurKey}"
        };

        foreach (var url in urls)
        {
            if (!url.Contains("imgur"))
            {
                continue;
            }

            var response = await HttpClient.GetAsync(url, cancellationToken);
            var soup = await Soupify(response, cancellationToken);
            var imgurUrl = soup.SelectSingleNodeOrThrow("//a[@id='image-link']")
                               .GetHref();
            var imageHash = imgurUrl.Split("#")[^1];
            var message = headers.ToRequest(HttpMethod.Get, $"https://api.imgur.com/3/image/{imageHash}");
            response = await HttpClient.SendAsync(message, cancellationToken);
            var responseJson = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            parsedUrls.Add(responseJson!["data"]!["link"]!.Deserialize<string>()!);
        }

        return parsedUrls;
    }
}