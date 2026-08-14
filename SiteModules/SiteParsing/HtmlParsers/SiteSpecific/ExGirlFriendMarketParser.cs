using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using OpenQA.Selenium;
using HtmlAgilityPack;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
// exgirlfirendmarket.com
public class ExGirlFriendMarketParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "exgirlfriendmarket";
    public static string[] SupportedUrls => ["https://www.exgirlfriendmarket.com/"];
    protected override string DirNameXpath => "//div[@class='title-area']//h1";
    protected override string ImageContainerXpath => "//div[@class='gallery']//a[@class='thumb exo']";

    public ExGirlFriendMarketParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ExGirlFriendMarketParser>(filenameScheme))
    {
    }
}