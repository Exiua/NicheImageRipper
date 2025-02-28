using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class XasiatParser : HtmlParser
{
    public XasiatParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for xasiat.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
        });
        var dirName = soup.SelectSingleNode("//div[@class='headline']/h1").InnerText;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/albums/"))
        {
            images = soup.SelectSingleNode("//div[@class='images']")
                            .SelectNodes("./a")
                            .Select(a => a.GetHref())
                            .ToStringImageLinkWrapperList();
        }
        else if (CurrentUrl.Contains("/videos/"))
        {
            var playButton = Driver.FindElement(By.XPath("//a[@class='fp-play']"));
            Driver.ScrollElementIntoView(playButton);
            Driver.Click(playButton);
            var exists = await WaitForElement("//video");
            if (!exists)
            {
                throw new RipperException("Video not found");
            }
    
            var player = Driver.FindElement(By.Id("kt_player"));
            var qualityButton = Driver.TryFindElement(By.XPath("//a[@class='fp-settings']"));
            if (qualityButton is not null)
            {
                var classes = player.GetDomAttribute("class")!;
                classes += " is-settings-open";
                Driver.ExecuteScript($"document.getElementById('kt_player').setAttribute('class', '{classes}')");
                var bestQuality = Driver.TryFindElement(By.XPath("//div[@class='fp-settings-list-item is-hd']/a"));
                if (bestQuality is not null)
                {
                    Driver.Click(bestQuality);
                }
                else
                {
                    Log.Warning("No HD quality found, using default");
                }
            }
            
            await Task.Delay(1000);
            var video = Driver.TryFindElement(By.XPath("//video"));
            var src = video!.GetSrc()!;
            images = [src];
        }
        else
        {
            throw new RipperException("Unknown URL type");
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
