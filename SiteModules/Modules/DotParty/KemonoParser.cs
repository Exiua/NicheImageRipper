using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

public class KemonoParser : DotPartyParser, IHtmlParser, IRefererOverrideHtmlParser
{
    public static string ParserName => "kemono";
    public static string[] SupportedUrls => ["https://kemono.party/", "https://kemono.su/", "https://kemono.cr/"];
    public static string RefererOverride => "";

    public KemonoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<KemonoParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for kemono.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        return DotPartyParse("https://kemono.cr", cancellationToken);
    }
}