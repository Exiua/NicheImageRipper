using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

public class PawchiveParser : DotPartyParser, IHtmlParser, IRefererOverrideHtmlParser
{
    public static string ParserName => "pawchive";
    public static string[] SupportedUrls { get; } = ["https://pawchive.pw/"];
    public static string RefererOverride => "";

    protected override string[] OwnHosts { get; } = SupportedUrls.Select(u => new Uri(u).Host).ToArray();

    public PawchiveParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<PawchiveParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for pawchive.pw and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        return DotPartyParse(cancellationToken);
    }
}