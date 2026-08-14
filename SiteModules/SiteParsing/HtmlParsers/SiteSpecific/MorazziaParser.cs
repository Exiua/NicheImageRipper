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
// morazzia.com
public class MorazziaParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "morazzia";
    public static string[] SupportedUrls => ["https://www.morazzia.com/"];
    protected override string DirNameXpath => "//h1[@class='title']";
    protected override string ImageContainerXpath => "//div[@class='block-post album-item']//a";

    public MorazziaParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MorazziaParser>(filenameScheme))
    {
    }
}