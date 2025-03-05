using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

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
        Log.Debug("Searching for continue button");
        var continueButton = Driver.TryFindElement(By.XPath("//input[@name='continue']"));
        if (continueButton is not null)
        {
            Log.Debug("Clicking continue button");
            continueButton.Click();
        }
        else
        {
            Log.Debug("Continue button not found");
        }

        var soup = await SolveParseAddCookies();
        var dirName = soup.SelectSingleNode("//div[@class='title']").InnerText;
        var videoPosts = new List<string>();
        while (true)
        {
            Log.Debug("Searching for videos");
            var videos = soup.SelectSingleNode("//div[@id='custom_list_videos_common_videos_items']")
                                .SelectNodes("./div")
                                .Select(div => div.SelectSingleNode("./a[@class='th js-open-popup']")?
                                                        .GetHref()
                                                        .DecodeUrl())
                                .Where(s => s is not null);
            videoPosts.AddRange(videos!);
            
            Log.Debug("Checking if there is a blockOverlay");
            var uiBlock = Driver.TryFindElement(By.XPath("//div[@class='blockUI blockOverlay']"));
            while (uiBlock is not null)
            {
                Log.Debug("Waiting for blockOverlay to disappear");
                await Task.Delay(500);
                uiBlock = Driver.TryFindElement(By.XPath("//div[@class='blockUI blockOverlay']"));
            }
            
            Log.Debug("Searching for next button");;
            var nextButton = Driver.TryFindElement(By.XPath("//div[@class='item pager next']/a"));
            if (nextButton is null)
            {
                Log.Debug("Next button not found");
                break;
            }
            
            Log.Debug("Clicking next button");  
            nextButton.Click();
            soup = await Soupify(delay: 500);
        }
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in videoPosts)
        {
            soup = await Soupify(post);
            Log.Debug("Searching for video info");
            var videoInfo = soup.SelectSingleNode("//div[@id='tab_video_info']");
            Log.Debug("Searching for downloads");
            var downloads = videoInfo.SelectNodes("./div")[^1];
            Log.Debug("Grabbing download link");
            // First link is the highest quality
            var downloadLink = downloads.SelectSingleNode(".//a").GetHref().DecodeUrl();
            images.Add(downloadLink);
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
