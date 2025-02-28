using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Rule34VideoParser : HtmlParser
{
    public Rule34VideoParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for rule34video.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        await Task.Delay(500);
        var continueButton = Driver.TryFindElement(By.XPath("//input[@name='continue']"));
        continueButton?.Click();
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='title']/span").InnerText;
        var videoPosts = new List<string>();
        while (true)
        {
            var videos = soup.SelectSingleNode("//div[@id='custom_list_videos_common_videos_items']")
                                .SelectNodes("./div")
                                .Select(div => div.SelectSingleNode("./a[@class='th js-open-popup']")?
                                                        .GetHref()
                                                        .DecodeUrl())
                                .Where(s => s is not null);
            videoPosts.AddRange(videos!);
            
            var uiBlock = Driver.TryFindElement(By.XPath("//div[@class='blockUI blockOverlay']"));
            while (uiBlock is not null)
            {
                await Task.Delay(500);
                uiBlock = Driver.TryFindElement(By.XPath("//div[@class='blockUI blockOverlay']"));
            }
            
            var nextButton = Driver.TryFindElement(By.XPath("//div[@class='item pager next']/a"));
            if (nextButton is null)
            {
                break;
            }
            nextButton.Click();
            soup = await Soupify(delay: 500);
        }
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in videoPosts)
        {
            soup = await Soupify(post);
            var videoInfo = soup.SelectSingleNode("//div[@id='tab_video_info']");
            var downloads = videoInfo.SelectNodes("./div")[^1];
            var downloadLink = downloads.SelectSingleNode(".//a").GetHref(); // First link is the highest quality
            images.Add(downloadLink);
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
