using System.Text.Json;
using NicheImageRipper.Sdk.Cache;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.Sdk.Utility;
using NicheImageRipper.SiteModules.Modules.DotParty.Models;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

public class CoomerParser : DotPartyParser, IHtmlParser, IRefererOverrideHtmlParser, ICacheOwner
{
    private const string ConstCachePath = "coomerCache.json";
    
    public static string ParserName => "coomer";
    public static string[] SupportedUrls => ["https://coomer.party/", "https://coomer.su/", "https://coomer.st/"];
    public static string RefererOverride => "";
    
    protected override string[] OwnHosts { get; } = SupportedUrls.Select(u => new Uri(u).Host).ToArray();
    protected override string CachePath => ConstCachePath;

    public CoomerParser(WebDriver driver,  Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, 
        requestHeaders, IHtmlParser.GetFilenameScheme<CoomerParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for coomer.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        return DotPartyParse(cancellationToken);
    }
    
    protected override DotPartyPostResponse DeserializePost(string rawJson) =>
        JsonSerializer.Deserialize<DotPartyPostResponse>(rawJson)
        ?? throw new RipperException("Failed to deserialize post");
 
    protected override string BuildFileUrl(string path, string? server, string domainUrl, DotPartyPostFull post) =>
        $"{server ?? domainUrl}/data{path}";

    public static void ClearCache()
    {
        FileUtility.SilentlyRemoveFile(ConstCachePath);
    }
}