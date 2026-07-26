using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class RabbitsFunParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "rabbitsfun";

    public RabbitsFunParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<RabbitsFunParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for rabbitsfun.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return GenericBabesHtmlParser("//h3[@class='watch-mobTitle']", "//div[@class='gallery-watch']//li");
    }
}
