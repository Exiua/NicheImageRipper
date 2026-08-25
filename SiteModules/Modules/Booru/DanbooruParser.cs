

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Booru;
public class DanbooruParser : BooruParser, IHtmlParser, ISubdomainSignificantHtmlParser
{
    public static string ParserName => "danbooru";
    public static string[] SupportedUrls => ["https://danbooru.donmai.us/"];
    public static int SignificantDomainLabels => 3;

    public DanbooruParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<DanbooruParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for danbooru.donmai.us and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Core.Enums.Booru.Danbooru, cancellationToken: cancellationToken);
    }
}