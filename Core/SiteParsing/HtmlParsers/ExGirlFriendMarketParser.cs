using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class ExGirlFriendMarketParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "exgirlfriendmarket";

    public ExGirlFriendMarketParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ExGirlFriendMarketParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for exgirlfriendmarket.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return await GenericBabesHtmlParser("//div[@class='title-area']//h1", "//div[@class='gallery']//a[@class='thumb exo']");
    }
}
