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
public class EightBoobsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "8boobs";
    public static string[] SupportedUrls => ["https://www.8boobs.com/"];

    public EightBoobsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EightBoobsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for 8boobs.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@id='content']").SelectNodesOrThrow(".//div[@class='title']")[1].InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='gallery clear']").SelectNodesOrThrow("./a").Select(img => Protocol + img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("tn_")).ToStringImageLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}