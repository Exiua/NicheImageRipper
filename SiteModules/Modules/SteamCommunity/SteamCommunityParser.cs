using Microsoft.AspNetCore.WebUtilities;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.Features;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.Sdk.Utility;
using SteamApiClientType = SteamApiClient.SteamApiClient;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SteamCommunity;

public class SteamCommunityParser : HtmlParser, IHtmlParser, INormalizingHtmlParser
{
    public static string ParserName => "steamcommunity";
    public static string[] SupportedUrls => ["https://steamcommunity.com/"];
    public static IReadOnlyList<(string Pattern, UrlMatchKind Kind)> NormalizationPatterns { get; } =
        [("steamcommunity.com", UrlMatchKind.Contains)];

    internal static SteamApiClientType SteamApiClient { get; } = new();

    public SteamCommunityParser(WebDriver driver,
                                Dictionary<string, string> requestHeaders,
                                FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<SteamCommunityParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for steamcommunity.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var available = AvailableFeatureManager.AvailableFeatures.HasFeature(FeatureKeys.SteamCmd,
            () => ProcessRunner.CheckForProcess("steamcmd", "+login anonymous +quit"));
        if (!available)
        {
            throw new FeatureNotAvailableException(FeatureKeys.SteamCmd);
        }
        
        var (username, password) = Config.Logins.GetValueOrDefault(ParserName).Deconstruct();
        if (username.IsNullOrEmpty())
        {
            throw new RipperException(
                "No username found for steamcommunity.com, cannot parse. Please provide a username in the config file.");
        }

        await SteamApiClient.LoginAsync(username, password ?? "", cancellationToken);
        ulong steamId;
        string dirName;
        if (CurrentUrl.Contains("/profiles/"))
        {
            var idString = CurrentUrl.Split("/")[4];
            steamId = ulong.Parse(idString);
            dirName = await SteamApiClient.GetPersonaNameAsync(steamId, cancellationToken);
        }
        else if (CurrentUrl.Contains("/id/"))
        {
            var vanityName = CurrentUrl.Split("/")[4];
            steamId = await SteamApiClientType.ResolveVanityUrlAsync(vanityName, cancellationToken);
            dirName = vanityName;
        }
        else
        {
            throw new RipperException($"Unknown url format provided: {CurrentUrl}");
        }

        var uri = new Uri(CurrentUrl);
        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue("appid", out var appIdString))
        {
            appIdString =
                "431960"; // This is mainly for getting Wallpaper Engine items, other types are not really supported atm
        }

        var appId = uint.Parse(appIdString!);
        var files = await SteamApiClient.GetUserWorkshopItemsAsync(steamId, appId, cancellationToken);
        var images = files.Select(file => file.publishedfileid)
                          .Select(fileId => FormatSteamWorkshopDownloadUrl(appId.ToString(), fileId.ToString()))
                          .Select(url =>
                               FileLink.WithDownloadResolvedFilename(url, FilenameScheme,
                                   linkInfo: SteamCommunityLinkInfo.SteamCommunity))
                          .Select(imageLink => (StringFileLinkWrapper)imageLink).ToList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private static string FormatSteamWorkshopDownloadUrl(string appId, string fileId)
    {
        // The URL doesn't matter, the extractor will use the appId and fileId to download the file using steamcmd.
        // This is mainly a hack to get around URL validation in ImageLink
        return $"https://steamcommunity.com/{appId}|{fileId}";
    }
    
    public static string NormalizeUrl(string url)
    {
        var parts = url.Split('?');
        var parameters = parts.Length > 1 ? parts[1].Split('&') : [];
        if (parameters.Length < 1)
        {
            throw new RipperException("Unexpected Steam Community URL format: " + url);
        }

        var appId = parameters.FirstOrDefault(p => p.StartsWith("appid"));
        if (appId is null)
        {
            throw new RipperException("Unexpected Steam Community URL format: " + url);
        }

        var normalizedUrl = $"{parts[0]}?{appId}";
        return normalizedUrl;
    }
}