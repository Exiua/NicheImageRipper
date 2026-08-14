using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class ShamelessParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "shameless";
    public static string[] SupportedUrls => ["https://shameless.com/"];

    public ShamelessParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ShamelessParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for shameless.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h4[@class='title']").InnerText;
        var script = soup.SelectSingleNodeOrThrow("//div[@class='player-holder']//script[not(@src)]").InnerText.Split("var flashvars = ")[1].Split("};")[0] + "}";
        var rawJson = ExtractJsonObject(script);
        var url = rawJson.Split("video_url:")[1].Split(",")[0].Trim().Trim('\'', '"');
        var images = new List<StringFileLinkWrapper>
        {
            url
        };
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}