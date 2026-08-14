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
public class HmvManiaParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hmvmania";
    public static string[] SupportedUrls => ["https://hmvmania.com/"];

    public HmvManiaParser(WebDriver driver, ApiClientManager apiClientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HmvManiaParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hmvmania.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='video-entry-title']").InnerText;
        var images = new List<StringFileLinkWrapper>();
        var videoUrl = soup.SelectSingleNodeOrThrow("//li[i[@class='fas fa-download']]/a").GetHref();
        images.Add(videoUrl);
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}