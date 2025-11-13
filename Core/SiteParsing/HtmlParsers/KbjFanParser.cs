using Common.ExtensionMethods;
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

public class KbjFanParser : HtmlParser
{
    public KbjFanParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for kbjfan.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='article-title']/a").InnerText;
        var images = new List<StringImageLinkWrapper>();
        for(var i = 0; i < 4; i++)
        {
            var success = await GetVideos(soup, images);
            if (success)
            {
                break;
            }

            if (i == 3)
            {
                throw new RipperException("Unable to find any videos on the page. " +
                                          "This may be due to a change in the site's structure or a temporary issue.");
            }

            Driver.ClearCookies();
            Driver.Refresh(true);
            await Sleep(250);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<bool> GetVideos(HtmlNode soup, List<StringImageLinkWrapper> images)
    {
        var videos = soup.SelectSingleNode("//div[contains(@class, 'featured-video-episode')]");
        if (videos is null)
        {
            Log.Debug("Single video found, extracting video link.");
            var video = soup.SelectSingleNode("//video")?.GetSrc();
            if (video is null or "https://cdn.plyr.io/static/blank.mp4")
            {
                return false;
            }
            
            images.Add(video);
        }
        else
        {
            Log.Debug("Multiple videos found, extracting all video links.");
            var index = 1;
            while (true)
            {
                var video = soup.SelectSingleNode("//video")?.GetSrc();
                if (video is null or "https://cdn.plyr.io/static/blank.mp4")
                {
                    return false;
                }
                
                images.Add(video);
                index++;
                var nextButton =
                    Driver.TryFindElement(
                        By.XPath($"//div[contains(@class, 'featured-video-episode')]/a[@data-index='{index}']"));
                if (nextButton is null)
                {
                    break;
                }
                
                nextButton.Click();
                soup = await Soupify(delay: 250);
            }
        }

        return true;
    }
}