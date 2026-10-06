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

public class PawchiveParser : DotPartyParser, IHtmlParser, IRefererOverrideHtmlParser, ICacheOwner
{
    private const string ConstCachePath = "pawchiveCache.json";
    
    public static string ParserName => "pawchive";
    public static string[] SupportedUrls { get; } = ["https://pawchive.pw/", "https://file.pawchive.pw/"];
    public static string RefererOverride => "";

    protected override string[] OwnHosts { get; } = SupportedUrls.Select(u => new Uri(u).Host).ToArray();
    protected override string CachePath => ConstCachePath;

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
    
    protected override DotPartyPostResponse DeserializePost(string rawJson)
    {
        var post = JsonSerializer.Deserialize<DotPartyPostFull>(rawJson)
                   ?? throw new RipperException("Failed to deserialize post");
        return new DotPartyPostResponse { Post = post };
    }

    protected override string BuildFileUrl(string path, string? server, string domainUrl) =>
        $"https://file.pawchive.pw/data{path}";
    
    public static void ClearCache()
    {
        FileUtility.SilentlyRemoveFile(ConstCachePath);
    }
}