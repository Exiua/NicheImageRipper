using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class SexHdParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "sexhd";

    public SexHdParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SexHdParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for sexhd.pics and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='photobig']//h4")
                            .InnerText
                            .Split(":")[1]
                            .Trim();
        var images = soup.SelectNodesOrThrow("//div[@class='photobig']//div[@class='relativetop']")
                            .Skip(1)
                            .Select(img => $"https://sexhd.pics{img.SelectSingleNodeOrThrow(".//a").GetHref()}")
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
