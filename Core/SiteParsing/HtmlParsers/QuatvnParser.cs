using Common.ExtensionMethods;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class QuatvnParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "quatvn";

    public QuatvnParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<QuatvnParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for quatvn.love and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        HtmlNode soup = null!;
        for (var i = 0; i < 4; i++)
        {
            await File.WriteAllTextAsync("test.html", Driver.PageSource);
            var startTime = DateTime.Now;
            soup = await Soupify(xpath: "//div[@class='fp-playlist']|//div[@class='fp-player']|//ul[@role='tablist']", xpathTimout: 120);
            var endTime = DateTime.Now;
            if (endTime - startTime < TimeSpan.FromSeconds(120))
            {
                break;
            }
            
            var gallery = Driver.TryFindElement(By.XPath("//div[@id='content']//div[@class='g1-content-narrow g1-typography-xl entry-content']//figure[@class='mace-gallery-teaser']"));
            if (gallery is not null)
            {
                break;
            }

            Logger.Debug("Unable to find playlist or tablist, refreshing page");
            if (i == 3)
            {
                throw new RipperException("Failed to load page");
            }
            
            Driver.Refresh();
        }
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='g1-mega g1-mega-1st entry-title']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var tablist = soup.SelectSingleNode("//ul[@role='tablist']");
        if (tablist is not null)
        {
            Logger.Debug("Parsing tabbed content");
            var tabs = tablist.SelectNodesOrThrow("./li");
            var numTabs = tabs.Count;
            var baseTabId = tablist.ParentNode.GetAttributeValue("id");
            Logger.Debug("Found {NumTabs} tabs", numTabs);
            for (var i = 0; i < numTabs; i++)
            {
                Logger.Debug("Parsing tab {TabNum}", i);
                var videoTab = soup.SelectSingleNodeOrThrow($"//div[@id='{baseTabId}-{i}']/div");
                var videoData = videoTab.GetAttributeValue("data-item");
                videoData = WebUtility.HtmlDecode(videoData);
                var videoList = JsonSerializer.Deserialize<JsonNode>(videoData)!.AsObject()["sources"]!.AsArray();
                var videos = videoList.Select(entry => entry!.AsObject()["src"]!.Deserialize<string>()!);
                images.AddRange(videos.Select(vid => (StringImageLinkWrapper)vid));
                if (i != numTabs - 1)
                {
                    var closeBtn = Driver.TryFindElement(By.XPath("//button[@class='close-btn']"));
                    closeBtn?.Click();
                    var nextTab = Driver.FindElement(By.XPath($"//li[@tabindex='{i}']"));
                    nextTab.Click();
                    soup = await Soupify();
                }
            }
        }
        else
        {
            Logger.Debug("Parsing non-tabbed content");
            var container =
                soup.SelectSingleNodeOrThrow(
                    "//div[@id='content']//div[@class='g1-content-narrow g1-typography-xl entry-content']");
            var gallery = container.SelectSingleNode(".//figure[@class='mace-gallery-teaser']");
            if (gallery is not null)
            {
                Logger.Debug("Parsing gallery");
                var imageData = gallery.GetAttributeValue("data-g1-gallery");
                imageData = WebUtility.HtmlDecode(imageData);
                var imageList = JsonSerializer.Deserialize<JsonNode>(imageData)!.AsArray();
                var imgs = imageList.Select(entry => entry!.AsObject()["full"]!.Deserialize<string>()!);
                images.AddRange(imgs.Select(img => (StringImageLinkWrapper)img));
            }

            var videoPlaylist = container.SelectSingleNode("//div[@class='fp-playlist']");
            if (videoPlaylist is not null)
            {
                Logger.Debug("Parsing video playlist");
                var videos = videoPlaylist.SelectNodesOrThrow("./a")
                                          .Select(a => a.GetHref())
                                          .ToStringImageLinks();
                images.AddRange(videos);
            }
            else
            {
                var player = container.SelectSingleNode("//div[@class='fp-player']");
                if (player is not null)
                {
                    var playerContainer = player.ParentNode;
                    var dataItem = playerContainer.GetAttributeValue("data-item");
                    dataItem = WebUtility.HtmlDecode(dataItem);
                    var videoData = JsonSerializer.Deserialize<JsonNode>(dataItem)!.AsObject()["sources"]!.AsArray();
                    var videos = videoData.Select(entry => entry!.AsObject()["src"]!.Deserialize<string>()!)
                                          .ToStringImageLinks();
                    images.AddRange(videos);
                }
            }
        }
        
        CleanTabs("quatvn.love");
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
