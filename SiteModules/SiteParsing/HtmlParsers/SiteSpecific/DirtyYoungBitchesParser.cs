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
public class DirtyYoungBitchesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "dirtyyoungbitches";
    public static string[] SupportedUrls => ["https://www.dirtyyoungbitches.com/"];
    protected override string DirNameXpath => "//div[@class='title-holder']//h1";
    protected override string ImageContainerXpath => "//div[@class='container cont-light']//div[@class='images']//a[@class='thumb']";

    public DirtyYoungBitchesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<DirtyYoungBitchesParser>(filenameScheme))
    {
    }
}