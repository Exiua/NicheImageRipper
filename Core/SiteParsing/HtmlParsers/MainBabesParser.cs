using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class MainBabesParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "mainbabes";

    public MainBabesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MainBabesParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for mainbabes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return GenericBabesHtmlParser("//div[@class='heading']//h2[@class='title']", "//div[@class='thumbs_box']//div[@class='thumb_box']");
    }
}
