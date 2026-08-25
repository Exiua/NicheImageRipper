

using Sdk.Common.ExtensionMethods;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Jpg5;
public class Jpg5Parser : ParameterizedHtmlParser, IHtmlParser
{
    public static string ParserName => "jpg5";
    public static string[] SupportedUrls => ["https://jpg5.su/"];

    public Jpg5Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Jpg5Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for jpg5.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        var single = false;
        var soup = await Soupify(cancellationToken: cancellationToken);
        var notFound = soup.SelectSingleNode("//div[@class='page-not-found']");
        if (notFound is not null)
        {
            Logger.Warning("Image not found");
            return RipInfo.Empty.WithDirectoryName("Not Found");
        }

        string? dirName;
        if (CurrentUrl.Contains("/a/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//a[@data-text='album-name']").InnerText;
        }
        else if (CurrentUrl.Contains("/img/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//a[@data-text='image-title']").InnerText;
            single = true;
        }
        else
        {
            dirName = soup.SelectSingleNodeOrThrow("//div[@class='header']").InnerText;
        }

        var images = new List<StringFileLinkWrapper>();
        if (!single)
        {
            var page = 1;
            while (true)
            {
                Logger.Information($"Parsing page {page}");
                page++;
                var error = soup.SelectSingleNode("//h1");
                if (error is not null && error.InnerText.StartsWith("500 I"))
                {
                    await Sleep(5000, cancellationToken); // Most likely due to rate limiting
                    Driver.Refresh();
                    soup = await Soupify(cancellationToken: cancellationToken);
                }

                var posts = soup.SelectSingleNodeOrThrow("//div[@class='pad-content-listing']").SelectNodesOrThrow("./div").Select(div => div.SelectSingleNodeOrThrow(".//img").GetSrc().Remove(".md")).ToStringFileLinks();
                images.AddRange(posts);
                var nextPage = soup.SelectSingleNode("//a[@data-pagination='next']");
                var nextPageUrl = nextPage?.GetNullableHref();
                if (nextPageUrl is null)
                {
                    break;
                }

                nextPageUrl = nextPageUrl.DecodeUrl();
                //Logger.Debug("Next page: {nextPageUrl}", nextPageUrl);
                soup = await Soupify(nextPageUrl, xpath: "//div[@class='pad-content-listing']/div", cancellationToken: cancellationToken);
            }
        }
        else
        {
            var img = soup.SelectSingleNode("//div[@id='image-viewer-container']/img");
            var imgSrc = img?.GetSrc();
            if (imgSrc is null || imgSrc.EndsWith("loading.svg"))
            {
                var downloadBtn = soup.SelectSingleNodeOrThrow("//a[@download]");
                var href = downloadBtn.GetHref();
                href = ObfuscationUtility.DeobfuscateJpg5Href(href);
                images.Add(href);
            }
            else
            {
                images.Add(imgSrc);
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}