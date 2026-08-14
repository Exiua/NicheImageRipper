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
// mainbabes.com
public class MainBabesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "mainbabes";
    public static string[] SupportedUrls => ["https://www.mainbabes.com/"];
    protected override string DirNameXpath => "//div[@class='heading']//h2[@class='title']";
    protected override string ImageContainerXpath => "//div[@class='thumbs_box']//div[@class='thumb_box']";

    public MainBabesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MainBabesParser>(filenameScheme))
    {
    }
}