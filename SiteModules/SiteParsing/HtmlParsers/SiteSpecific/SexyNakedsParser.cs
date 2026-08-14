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
// sexynakeds.com
public class SexyNakedsParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "sexynakeds";
    public static string[] SupportedUrls => ["https://www.sexynakeds.com/"];
    protected override string DirNameXpath => "(//div[@class='box']//h1)[2]";
    protected override string ImageContainerXpath => "//div[@class='post_tn']";

    public SexyNakedsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SexyNakedsParser>(filenameScheme))
    {
    }
}