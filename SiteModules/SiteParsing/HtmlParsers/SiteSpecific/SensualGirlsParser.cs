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
// sensualgirls.org
public class SensualGirlsParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "sensualgirls";
    public static string[] SupportedUrls => ["https://www.sensualgirls.org/"];
    protected override string DirNameXpath => "//a[@class='albumName']";
    protected override string ImageContainerXpath => "//div[@id='box_289']//div[@class='gbox']";

    public SensualGirlsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SensualGirlsParser>(filenameScheme))
    {
    }
}