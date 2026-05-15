using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.FileDownloading;
using Core.Managers;
using Core.Utility;
using HtmlAgilityPack;
using Microsoft.AspNetCore.WebUtilities;
using OpenQA.Selenium;
using Serilog;
using NotSupportedException = Core.Exceptions.NotSupportedException;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SteamCommunityParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "steamcommunity";

    public SteamCommunityParser(WebDriver driver, ApiClientManager apiClientManager,
                                Dictionary<string, string> requestHeaders,
                                FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,
        apiClientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SteamCommunityParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for steamcommunity.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var (username, password) = Config.Logins.SteamCommunity;
        if (username.IsNullOrEmpty())
        {
            throw new RipperException(
                "No username found for steamcommunity.com, cannot parse. Please provide a username in the config file.");
        }

        var client = ImageRipper.ClientManager.SteamApiClient;
        await client.LoginAsync(username, password);
        ulong steamId;
        string dirName;
        if (CurrentUrl.Contains("/profiles/"))
        {
            var idString = CurrentUrl.Split("/")[4];
            steamId = ulong.Parse(idString);
            dirName = await client.GetPersonaNameAsync(steamId);
        }
        else if (CurrentUrl.Contains("/id/"))
        {
            var vanityName = CurrentUrl.Split("/")[4];
            steamId = await client.ResolveVanityUrlAsync(vanityName);
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
        var files = await client.GetUserWorkshopItemsAsync(steamId, appId);
        var images = files.Select(file => file.publishedfileid)
                          .Select(fileId => FormatSteamWorkshopDownloadUrl(appId.ToString(), fileId.ToString()))
                          .Select(url => new ImageLink(url, FilenameScheme, 0)
                               { Filename = "discard", LinkInfo = LinkInfo.SteamCommunity, })
                          .Select(imageLink => (StringImageLinkWrapper)imageLink).ToList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private static string FormatSteamWorkshopDownloadUrl(string appId, string fileId)
    {
        // The URL doesn't matter, the extractor will use the appId and fileId to download the file using steamcmd.
        // This is mainly a hack to get around URL validation in ImageLink
        return $"https://steamcommunity.com/{appId}|{fileId}";
    }
}