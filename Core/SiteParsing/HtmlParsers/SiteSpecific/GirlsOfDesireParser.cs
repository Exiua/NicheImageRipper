using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class GirlsOfDesireParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "girlsofdesire";

    public GirlsOfDesireParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<GirlsOfDesireParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for girlsofdesire.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return GenericBabesHtmlParser("//a[@class='albumName']", "//div[@id='gal_10']//td[@class='vtop']");
    }
}
