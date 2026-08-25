using NicheImageRipper.Common.ExtensionMethods;

using PixivApi;
using PixivApi.Utilities;
using Sdk.Common;
using Sdk.Common.ExtensionMethods;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Pixiv;

public class PixivParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "pixiv";
    public static string[] SupportedUrls => ["https://www.pixiv.net/"];

    private PixivApiClient PixivClient { get; set; } = new();

    public PixivParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                       FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<PixivParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for pixiv.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        const int delay = 500;
        const int illustsPerPage = 30;
        var cookies = Config.Cookies.GetValueOrDefault(ParserName, []);
        if (cookies.Length == 0)
        {
            Logger.Error("Pixiv session ID is not set. Please add your Pixiv PHPSESSID token to the config file.");
            return RipInfo.Empty;
        }

        var refreshToken = Config.Keys.GetValueOrDefault(ParserName, "");
        if (refreshToken == "")
        {
            Logger.Error("Pixiv refresh token is not set. Please add your Pixiv refresh token to the config file.");
            return RipInfo.Empty;
        }

        var client = PixivClient;
        _ = await client.Auth(refreshToken, cancellationToken: cancellationToken);
        var artistId = CurrentUrl.Split("/")[5];
        var userIllusts = await client.UserIllusts(artistId, cancellationToken: cancellationToken);
        var dirName = $"[Pixiv] {userIllusts.User.Name} ({artistId})";
        var images = new List<StringFileLinkWrapper>();
        var page = 0;
        while (true)
        {
            page++;
            Logger.Information("Parsing page {Page}", page);
            foreach (var illust in userIllusts.Illusts)
            {
                switch (illust.Type)
                {
                    case "illust":
                    {
                        if (illust.PageCount == 1)
                        {
                            var url = illust.MetaSinglePage.OriginalImageUrl!;
                            images.Add(url);
                        }
                        else
                        {
                            for (var i = 0; i < illust.PageCount; i++)
                            {
                                var url = illust.MetaPages[i].ImageUrls.Large.ToOriginalUrl();
                                images.Add(url);
                            }
                        }

                        break;
                    }

                    case "ugoira":
                    {
                        var illustId = illust.Id;
                        var filename = $"{illustId}.webp";
                        var url = $"https://www.pixiv.net/artworks/{illustId}";
                        var img = FileLink.WithFilename(url, filename, FilenameScheme,
                            linkInfo: PixivUgoiraLinkInfo.PixivUgoira);
                        images.Add(img);
                        break;
                    }

                    default:
                        Logger.Warning("Unknown/unsupported illustration type: {Type}", illust.Type);
                        break;
                }

                var description = illust.Caption;
                if (!string.IsNullOrWhiteSpace(description))
                {
                    var descHtml = $"<div>{description}</div>";
                    var soup = await Soupify(descHtml, urlString: false, cancellationToken: cancellationToken);
                    var links = soup.SelectNodesSafe("./a").Select(a => a.GetHref())
                                    .Select(href => href.Remove("/jump.php?")).Select(Uri.UnescapeDataString)
                                    .Where(ExternalLinkExtractor.UrlCanBeParsed).ToStringFileLinks();
                    images.AddRange(links);
                }
            }

            if (userIllusts.Illusts.Count < illustsPerPage)
            {
                break;
            }

            userIllusts = await client.UserIllusts(artistId, offset: page * illustsPerPage,
                cancellationToken: cancellationToken);
            await Sleep(delay, cancellationToken);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}