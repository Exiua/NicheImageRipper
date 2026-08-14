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
public class HentaiClubParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hentaiclub";
    public static string[] SupportedUrls => ["https://www.hentaiclub.net/"];

    public HentaiClubParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HentaiClubParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hentaiclub.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        await LazyLoad(new LazyLoadArgs { ScrollBy = true, Increment = 1250 });
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//span[@class='post-info-text']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@id='masonry']").SelectNodesOrThrow("./div").Select(div => div.SelectSingleNodeOrThrow("./img").GetSrc()).ToStringImageLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}