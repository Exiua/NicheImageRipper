using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class FoamGirlParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "foamgirl";
    public static string[] SupportedUrls => ["https://foamgirl.net/"];

    public FoamGirlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<FoamGirlParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for foamgirl.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='item_title']/h1").InnerText.Split('(')[0];
        var images = new List<StringFileLinkWrapper>();
        var pageContainer = soup.SelectSingleNode("//div[@class='nav-links page_imges']/a[@title='Last']") ?? soup.SelectSingleNodeOrThrow("//div[@class='nav-links page_imges']").SelectNodesOrThrow("./a")[^2];
        var pageCount = pageContainer.InnerText.ParseInt();
        var baseUrl = CurrentUrl;
        for (var i = 0; i < pageCount; i++)
        {
            var imgs = soup.SelectSingleNodeOrThrow("//div[@id='image_div']/p").SelectNodesOrThrow("./a").Select(a => a.GetHref()).ToStringImageLinks();
            images.AddRange(imgs);
            if (i != pageCount - 1)
            {
                soup = await Soupify(baseUrl.Replace(".html", $"_{i + 2}.html"));
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}