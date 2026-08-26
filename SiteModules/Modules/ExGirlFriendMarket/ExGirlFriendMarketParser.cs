
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.ExGirlFriendMarket;
// exgirlfirendmarket.com
public class ExGirlFriendMarketParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "exgirlfriendmarket";
    public static string[] SupportedUrls => ["https://www.exgirlfriendmarket.com/"];
    protected override string DirNameXpath => "//div[@class='title-area']//h1";
    protected override string ImageContainerXpath => "//div[@class='gallery']//a[@class='thumb exo']";

    public ExGirlFriendMarketParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<ExGirlFriendMarketParser>(filenameScheme))
    {
    }
}