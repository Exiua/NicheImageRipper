using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PixivParser : HtmlParser
{
    public PixivParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                       FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for pixiv.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        const int delay = 500;
        const int illustsPerPage = 30;
        
        if (Config.Cookies.Pixiv.Length == 0)
        {
            Log.Error("Pixiv session ID is not set. Please add your Pixiv PHPSESSID token to the config file.");
            return RipInfo.Empty;
        }

        if (Config.Keys.Pixiv == "")
        {
            Log.Error("Pixiv refresh token is not set. Please add your Pixiv refresh token to the config file.");
            return RipInfo.Empty;
        }

        var refreshToken = Config.Keys.Pixiv;
        var client = ApiClientManager.PixivClient;
        _ = await client.Auth(refreshToken);
        var artistId = CurrentUrl.Split("/")[5];
        var userIllusts = await client.UserIllusts(artistId);
        var dirName = $"[Pixiv] {userIllusts.User.Name} ({artistId})";
        var images = new List<StringImageLinkWrapper>();
        var page = 0;
        while(true)
        {
            page++;
            Log.Information("Parsing page {Page}", page);
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
                        var img = new ImageLink(url, FilenameScheme, 0, linkInfo: LinkInfo.PixivUgoira,
                            filename: filename);
                        images.Add(img);

                        break;
                    }
                    default:
                        Log.Warning("Unknown/unsupported illustration type: {Type}", illust.Type);
                        break;
                }

                var description = illust.Caption;
                if (!string.IsNullOrWhiteSpace(description))
                {
                    var descHtml = $"<div>{description}</div>";
                    var soup = await Soupify(descHtml, urlString: false);
                    var links = soup.SelectNodesSafe("./a")
                                    .Select(a => a.GetHref())
                                    .Select(href => href.Remove("/jump.php?"))
                                    .Select(Uri.UnescapeDataString)
                                    .Where(UrlCanBeParsed)
                                    .ToStringImageLinks();
                    images.AddRange(links);
                }
            }

            if (userIllusts.Illusts.Count < illustsPerPage)
            {
                break;
            }
            
            userIllusts = await client.UserIllusts(artistId, offset: page * illustsPerPage);
            await Sleep(delay);
        }
                
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}