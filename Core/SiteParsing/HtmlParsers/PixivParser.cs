using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Core.Managers;
using OpenQA.Selenium;
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
    public override Task<RipInfo> Parse()
    {
        return Parse2();
    }
    
    private async Task<RipInfo> Parse1()
    {
        const int delay = 500;
        if (Config.Cookies.Pixiv.Length == 0)
        {
            Log.Error("Pixiv session ID is not set. Please add your Pixiv PHPSESSID token to the config file.");
            return RipInfo.Empty;
        }

        // Todo: Add cookie rotation for multiple accounts
        var sessionId =
            TokenManager.GetTokenWithRotation(RotationKey.Pixiv, TimeSpan.FromHours(24), Config.Cookies.Pixiv);
        Driver.SetCookie("PHPSESSID", sessionId);
        if (!CurrentUrl.EndsWith("/artworks"))
        {
            var artworksUrl = CurrentUrl.Split('/').Take(6).Join("/") + "/artworks";
            CurrentUrl = artworksUrl;
        }
        else
        {
            Driver.Refresh();
        }

        const string xpathToFind = "//ul[@class='sc-bf8cea3f-1 bCxfvI']/li";
        var soup = await Soupify(delay: delay, xpath: xpathToFind);
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
        var posts = new List<string>();
        var page = 1;
        while (true)
        {
            Log.Information("Parsing page {Page}", page);
            page++;
            var p = soup.SelectNodesOrThrow("//ul/li[@size]")
                        .Select(li => li.SelectSingleNodeOrThrow(".//a").GetHref())
                        .Select(href => $"https://www.pixiv.net{href}");
            posts.AddRange(p);

            var lastNavButton = soup.SelectSingleNodeOrThrow("//nav[button]")
                                    .SelectNodesOrThrow("./a")
                                    .Last();
            var icon = lastNavButton.SelectSingleNode("./svg");
            var disabled = lastNavButton.GetAttributeValue("aria-disabled") == "true";
            if (icon is null || disabled)
            {
                break;
            }

            var nextPageUrl = "https://www.pixiv.net" + lastNavButton.GetHref();
            soup = await Soupify(nextPageUrl, delay: delay, xpath: xpathToFind);
        }

        const string xpathToFindImages = "//div[@role='presentation']";
        const string xpathToFindDescription = "//p[starts-with(@id, 'expandable-paragraph-')]";
        //const string xpathToFindDescription = "//div[@class='sc-9f87882a-14 fZWCmd']";
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in posts)
        {
            Log.Information("Parsing post {Post}", post);
            ViewType viewType;
            CurrentUrl = post;
            while (true)
            {
                await Sleep(delay);
                viewType = ViewType.Normal;
                var errorH1 = Driver.TryFindElement(By.XPath("//h1"));
                if (errorH1 is null)
                {
                    break;
                }

                var errorText = errorH1.Text;
                if (!errorText.StartsWith("An error"))
                {
                    break;
                }

                Log.Warning("Encountered an error page. Retrying...");
                await Sleep(60000);
                Driver.Refresh();
            }

            var buttonClicked = false;
            var canvas = Driver.TryFindElement(By.TagName("canvas"));
            if (canvas is not null)
            {
                viewType = ViewType.Ugoira;
            }
            else
            {
                var showButton = Driver.TryFindElement(By.XPath("//div[@class='sc-9222a8f6-2 eVaEhv']"));
                if (showButton is not null)
                {
                    var buttonText = showButton.Text;
                    if (buttonText == "Reading works")
                    {
                        viewType = ViewType.Webtoon;
                    }

                    showButton.Click();
                    buttonClicked = true;
                    await Sleep(delay);
                }
            }

            var found = await WaitForElement(xpathToFindDescription);
            if (found is not null)
            {
                Log.Debug("Description not found for post {Post}", post);
            }

            //DebugUtility.Pause();
            soup = await Soupify(xpath: xpathToFindImages);
            var currentCount = images.Count;
            switch (viewType)
            {
                case ViewType.Normal:
                {
                    var imgs = soup.SelectSingleNodeOrThrow("//div[@role='presentation']")
                                   .SelectNodesOrThrow(".//a")
                                   .Select(a => a.GetHref())
                                   .ToStringImageLinks();
                    images.AddRange(imgs);
                    break;
                }
                case ViewType.Webtoon:
                {
                    var imgs = soup.SelectSingleNodeOrThrow("//div[@class='sc-e06c24aa-1 edbfOL']")
                                   .SelectNodesOrThrow(".//a")
                                   .Select(a => a.GetHref())
                                   .ToStringImageLinks();
                    images.AddRange(imgs);
                    break;
                }
                case ViewType.Ugoira:
                    var illustId = post.Split('/')[5];
                    var filename = $"{illustId}.webp";
                    var img = new ImageLink(post, FilenameScheme, 0, linkInfo: LinkInfo.PixivUgoira,
                        filename: filename);
                    images.Add(img);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (buttonClicked)
            {
                if (images.Count - currentCount < 2)
                {
                    Log.Warning("Parser may have missed images for {Post}", post);
                }
            }

            var description = soup.SelectSingleNode(xpathToFindDescription);
            if (description is not null)
            {
                Log.Debug("Description found for post {Post}", post);
                var links = description.SelectNodesSafe("./a")
                                       .Select(a => a.GetHref())
                                       .Select(href => href.Remove("/jump.php?"))
                                       .Select(Uri.UnescapeDataString)
                                       .Where(UrlCanBeParsed)
                                       .ToStringImageLinks();
                images.AddRange(links);
            }
        }

        TokenManager.UpdateTokenRotation(RotationKey.Pixiv);
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<RipInfo> Parse2()
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
        var test = await client.Auth(refreshToken);
        var artistId = CurrentUrl.Split("/")[5];
        var userIllusts = await client.UserIllusts(artistId);
        var dirName = userIllusts.User.Name;
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

    private enum ViewType
    {
        Normal,
        Webtoon,
        Ugoira,
    }
}