using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class KaiztyParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "kaizty";

    public KaiztyParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<KaiztyParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for kaizty.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='c-denomination s-denomination']//h2").InnerText;
        var end = dirName.IndexOf(" |", StringComparison.Ordinal);
        const int start = 15; // Length of "Kaizty Photos: "
        dirName = end < 0 ? dirName[start..] : dirName[start..end];
        var images = new List<StringFileLinkWrapper>();
        while (true)
        {
            var imgs = soup.SelectSingleNodeOrThrow("//div[@class='contentme']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => img.GetSrc().Split("?")[0])
                            .Where(link => link.StartsWith("https"))
                            .ToStringImageLinks();
            images.AddRange(imgs);
    
            var pagination = soup.SelectSingleNode("//ul[@class='pagination-site']");
            var nextPage = pagination?.SelectNodesOrThrow(".//a")
                                        .Where(a => a.InnerText.StartsWith("Next"))
                                        .Select(a => a.GetHref())
                                        .FirstOrDefault();
            if (nextPage is null)
            {
                break;
            }
            
            soup = await Soupify($"https://www.kaizty.com{nextPage}");
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
