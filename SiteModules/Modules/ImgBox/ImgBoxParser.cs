

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.ImgBox;
public class ImgBoxParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "imgbox";
    public static string[] SupportedUrls => ["https://imgbox.com/"];

    public ImgBoxParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ImgBoxParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for imgbox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@id='gallery-view']").SelectSingleNodeOrThrow(".//h1").InnerText.Split(" - ")[0];
        var images = soup.SelectSingleNodeOrThrow("//div[@id='gallery-view-content']").SelectNodesOrThrow(".//img").Select(img => img.GetSrc().Replace("thumbs2", "images2").Replace("_b", "_o")).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}