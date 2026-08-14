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
public class BabesInPornParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "babesinporn";
    public static string[] SupportedUrls => ["https://www.babesinporn.com/"];
    protected override string DirNameXpath => "//h1[@class='blockheader pink center lowercase']";
    protected override string ImageContainerXpath => "//div[@class='list gallery']";

    public BabesInPornParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabesInPornParser>(filenameScheme))
    {
    }
}