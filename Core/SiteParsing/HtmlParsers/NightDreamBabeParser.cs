using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class NightDreamBabeParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "nightdreambabe";

    public NightDreamBabeParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NightDreamBabeParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for nightdreambabe.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return GenericBabesHtmlParser("//section[@class='outer-section']//h2[@class='section-title title']",
            "//div[@class='lightgallery thumbs quadruple fivefold']//a[@class='gallery-card']");
    }
}
