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
public class HotGirlParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hotgirl";
    public static string[] SupportedUrls => ["https://hotgirl.asia/"];

    public HotGirlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HotGirlParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hotgirl.asia and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        if (!CurrentUrl.Contains("stype=slideshow"))
        {
            var urlParts = CurrentUrl.Split("/")[..4];
            CurrentUrl = "/".Join(urlParts) + "/?stype=slideshow";
        }

        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h3[@itemprop='name']").InnerText;
        var images = soup.SelectNodesOrThrow("//img[@class='center-block w-100']").Select(image => image.GetSrc()).ToStringImageLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}