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
// grabpussy.com
public class GrabPussyParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "grabpussy";
    public static string[] SupportedUrls => ["https://www.grabpussy.com/"];
    protected override string DirNameXpath => "(//div[@class='c-title'])[2]//h1";
    protected override string ImageContainerXpath => "//div[@class='gal own-gallery-images']/a";

    public GrabPussyParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<GrabPussyParser>(filenameScheme))
    {
    }
}