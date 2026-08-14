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
public class BabesBangParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "babesbang";
    public static string[] SupportedUrls => ["https://www.babesbang.com/"];
    protected override string DirNameXpath => "//div[@class='main-title']";
    protected override string ImageContainerXpath => "//div[@class='gal-block']";

    public BabesBangParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabesBangParser>(filenameScheme))
    {
    }
}