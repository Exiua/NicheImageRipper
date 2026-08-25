using HtmlAgilityPack;


using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.Exceptions;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Cosblay;
public class CosblayParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "cosblay";
    public static string[] SupportedUrls => ["https://cosblay.com/", "https://en.cosblay.com/"];

    public CosblayParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<CosblayParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for cosblay.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs, delay: 250, cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='entry-title']").InnerText;
        var images = new List<StringFileLinkWrapper>();
        var pageCount = 1;
        while (true)
        {
            Logger.Information("Page {PageCount}", pageCount++);
            var imageContainers = soup.SelectSingleNodeOrThrow("//div[@class='entry-content']/p").SelectNodes(".//img");
            if (imageContainers is null)
            {
                var nextBtn = GetNextButton(soup);
                if (nextBtn is null)
                {
                    break;
                }

                throw new RipperException("Images not found");
            }

            var imgs = imageContainers.Select(img =>
            {
                var src = img.GetNullableSrc();
                return src ?? img.ParentNode.GetHref();
            }).ToStringFileLinks();
            images.AddRange(imgs);
            var nextButton = GetNextButton(soup);
            if (nextButton is null)
            {
                break;
            }

            soup = await Soupify(nextButton.GetHref(), lazyLoadArgs: lazyLoadArgs, delay: 250, cancellationToken: cancellationToken);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
        // ReSharper disable once VariableHidesOuterVariable
        HtmlNode? GetNextButton(HtmlNode soup)
        {
            var pager = soup.SelectSingleNode("//div[@class='pgntn-page-pagination-block']");
            var nextButton = pager?.SelectNodesOrThrow("./a").FirstOrDefault(a => a.InnerText.StartsWith("Next"));
            return nextButton;
        }
    }
}