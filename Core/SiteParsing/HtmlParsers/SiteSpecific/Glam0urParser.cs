using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class Glam0urParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "glam0ur";

    public Glam0urParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Glam0urParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for glam0ur.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return GenericBabesHtmlParser("//div[@class='picnav']//h1", "//div[@class='center']/a");
    }
}
