using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using HtmlAgilityPack;
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
        Driver.SetCookie("steamLoginSecure", Config.Cookies.SteamCommunity);
        Driver.Refresh();
        var soup = await Soupify();
        string dirName;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/myworkshopfiles/"))
        {
            dirName  = soup.SelectSingleNodeOrThrow("//span[@id='HeaderUserInfoName']/a").InnerText;
            var itemPosts = new List<string>();
            var pageCount = 1;
            while (true)
            {
                Log.Information("Parsing page {PageCount} of workshop items...", pageCount);
                pageCount++;
                var items = soup.SelectSingleNodeOrThrow("//div[@class='workshopBrowseItems']")
                                .SelectNodesOrThrow("./div");
                itemPosts.AddRange(items.Select(item => item.SelectSingleNodeOrThrow("./a").GetHref()));
                var nextButton = Driver.FindElement(By.XPath("//div[@class='workshopBrowsePagingControls']/*[contains(@class, 'pagebtn')][last()]"));
                if(nextButton.GetAttribute("class")!.Contains("disabled"))
                {
                    break;
                }
                
                nextButton.Click();
                soup = await Soupify(delay: 500);
            }

            images = [];
            foreach (var (i, post) in itemPosts.Enumerate())
            {
                Log.Information("Parsing workshop item {ItemIndex}/{TotalItems}...", i + 1, itemPosts.Count);
                soup = await Soupify(post, delay: 250);
                var (_, url) = ExtractUrl(soup);
                var imageLink = new ImageLink(url, FilenameScheme, 0)
                {
                    Filename = "discard",
                    LinkInfo = LinkInfo.SteamCommunity,
                };
                images.Add(imageLink);
            }
        }
        else if (CurrentUrl.Contains("/sharedfiles/"))
        {
            (dirName, var url) = ExtractUrl(soup);
            var imageLink = new ImageLink(url, FilenameScheme, 0)
            {
                Filename = "discard",
                LinkInfo = LinkInfo.SteamCommunity,
            };
            images = [imageLink];
        }
        else
        {
            throw new NotSupportedException("SteamCommunityParser", $"Url not supported: {CurrentUrl}");
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private (string, string) ExtractUrl(HtmlNode soup)
    {
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='workshopItemTitle']").InnerText;
        var appId = soup.SelectSingleNodeOrThrow("//div[@class='breadcrumbs']/a").GetHref().Split("/")[^1];
        var fileId = CurrentUrl.Split("id=")[^1].Split("&")[0];
        return (dirName, FormatSteamWorkshopDownloadUrl(appId, fileId));
    }
    
    private static string FormatSteamWorkshopDownloadUrl(string appId, string fileId)
    {
        // The URL doesn't matter, the extractor will use the appId and fileId to download the file using steamcmd.
        // This is mainly a hack to get around URL validation in ImageLink
        return $"https://steamcommunity.com/{appId}|{fileId}";
    }
}