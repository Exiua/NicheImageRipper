

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Booru;
public class E621Parser : BooruParser, IHtmlParser
{
    public static string ParserName => "e621";
    public static string[] SupportedUrls => ["https://e621.net/"];

    public E621Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<E621Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for e621.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Core.Enums.Booru.E621, cancellationToken: cancellationToken);
    }
}