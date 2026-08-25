

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Hdzog;
public class HdzogParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hdzog";
    public static string[] SupportedUrls => ["https://hdzog.com/"];

    public HdzogParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HdzogParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hdzog.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var id = CurrentUrl.Split("/")[4];
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='title-h1']").InnerText + $" ({id})";
        var images = new List<StringFileLinkWrapper>();
        var url = soup.SelectSingleNodeOrThrow("//video[@class='jw-video jw-reset']").GetSrc();
        images.Add("https://hdzog.com" + url);
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}