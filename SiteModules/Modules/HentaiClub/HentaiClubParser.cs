

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.HentaiClub;
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
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        await LazyLoad(new LazyLoadArgs { ScrollBy = true, Increment = 1250 }, cancellationToken: cancellationToken);
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//span[@class='post-info-text']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@id='masonry']").SelectNodesOrThrow("./div").Select(div => div.SelectSingleNodeOrThrow("./img").GetSrc()).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}