using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class NakedGirlsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "nakedgirls";

    public NakedGirlsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NakedGirlsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for nakedgirls.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='content']")
                            .SelectSingleNodeOrThrow(".//h1")
                            .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='content']")
                            .SelectNodesOrThrow(".//div[@class='thumb']")
                            .Select(img => "https://www.nakedgirls.xxx" + img.SelectSingleNodeOrThrow(".//a").GetHref())
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
