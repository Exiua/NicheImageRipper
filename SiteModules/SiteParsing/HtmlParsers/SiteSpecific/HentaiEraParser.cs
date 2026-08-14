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
public class HentaiEraParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hentaiera";
    public static string[] SupportedUrls => ["https://hentaiera.com/"];

    public HentaiEraParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HentaiEraParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hentaiera.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='row gallery_first']/h1").InnerText;
        var pageCount = soup.SelectSingleNodeOrThrow("//button[@id='pages_btn']").InnerText.Trim().Split(' ')[0].ParseInt();
        var imageContainer = soup.SelectSingleNode("//img[@class='lazy filtered entered loaded']") ?? soup.SelectSingleNodeOrThrow("//img[@class='lazy entered loaded']");
        var baseUrl = imageContainer.GetAttributeValue("data-src");
        return RipInfo.FromGenerateInfo(baseUrl, dirName, pageCount);
    }
}