using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class BaobuaParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "baobua";

    public BaobuaParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders,
        IHtmlParser.GetFilenameScheme<BaobuaParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for baobua.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//span[@itemprop='name']").InnerText.Split("|")[0].Trim();
        var images = new List<StringImageLinkWrapper>();
        var pageCount = soup.SelectSingleNode("//div[@class='nav-links']")?.LastChild.InnerText.ParseInt() ?? 1;
        var baseUrl = CurrentUrl;
        for(var i = 0; i < pageCount; i++)
        {
            var imgs = soup.SelectSingleNodeOrThrow("//div[@class='entry-content read-details']")
                           .SelectNodesOrThrow("./figure")
                           .Select(figure => figure.SelectSingleNodeOrThrow("./a").GetHref())
                           .ToStringImageLinks();
            images.AddRange(imgs);
            if (i != pageCount - 1)
            {
                soup = await Soupify($"{baseUrl}?p={i + 2}");
            }
        }
        
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}