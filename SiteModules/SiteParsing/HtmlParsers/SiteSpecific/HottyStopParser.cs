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
public class HottyStopParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hottystop";
    public static string[] SupportedUrls => ["https://www.hottystop.com/"];

    public HottyStopParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HottyStopParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hottystop.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var boxLargeContent = soup.SelectSingleNodeOrThrow("//div[@class='content-center content-center-2']");
        var titleNode = boxLargeContent.SelectSingleNode(".//h1") ?? boxLargeContent.SelectSingleNodeOrThrow(".//u");
        var dirName = titleNode.InnerText;
        var images = soup.SelectSingleNodeOrThrow("//ul[@class='gallery']").SelectNodesOrThrow(".//a").Select(a => a.GetHref()).ToStringImageLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}