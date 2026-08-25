

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Booru;
public class GelbooruParser : BooruParser, IHtmlParser
{
    public static string ParserName => "gelbooru";
    public static string[] SupportedUrls => ["https://gelbooru.com/"];

    public GelbooruParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<GelbooruParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for gelbooru.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Core.Enums.Booru.Gelbooru, cancellationToken: cancellationToken);
    }
}