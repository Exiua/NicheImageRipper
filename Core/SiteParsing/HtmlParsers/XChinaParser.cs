using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using HtmlAgilityPack;
using OpenQA.Selenium;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class XChinaParser : HtmlParser
{
    public XChinaParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for en.xchina.co and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var tabContents = soup.SelectSingleNode("//div[@class='tab-content video-info']");
        var publisherNode = tabContents.SelectSingleNode(".//i[@class='fa fa-video-camera']") 
                            ?? tabContents.SelectSingleNode(".//i[@class='fa fa-user-circle']");
    
        var publisher = publisherNode.ParentNode.SelectSingleNode(".//a").InnerText;
        
        var series = tabContents.SelectSingleNode(".//i[@class='fa fa-file-o']");
        var id = series is not null ? series.ParentNode.InnerText : CurrentUrl.Split("id-")[^1].Split(".")[0];
        var title = tabContents.SelectNodes(".//div")[0].InnerText;
        var dirName = $"[{publisher}] {id} - {title}";
        var images = new List<StringImageLinkWrapper>();
        if (CurrentUrl.Contains("/video/"))
        {
            var url = GetVideoUrl(soup);
            images.Add(url);
        }
        else
        {
            int numVids;
            var controls = soup.SelectSingleNode("//div[@class='controls']");
            if (controls is not null)
            {
                var index = controls.SelectSingleNode(".//div[@class='index']")
                                    .InnerText
                                    .Split(" ")[^1];
                numVids = int.Parse(index);
            }
            else
            {
                var vid = soup.SelectSingleNode("//div[@class='container']//video");
                numVids = vid is not null ? 1 : 0;
            }
    
            var prevUrl = "";
            for (var i = 0; i < numVids; i++)
            {
                var vidSrc = GetVideoUrl(soup);
                while (vidSrc == prevUrl)
                {
                    await Task.Delay(250);
                    // var vidNode = Driver.FindElement(By.XPath("//video"));
                    // vidSrc = vidNode.GetSrc()!;
                    soup = await Soupify();
                    vidSrc = GetVideoUrl(soup);
                }
                
                images.Add(vidSrc);
                prevUrl = vidSrc;
                var nextButton = Driver.TryFindElement(By.XPath("//div[@go='1']"));
                nextButton?.Click();
            }
    
            while (true)
            {
                var photos = soup.SelectSingleNode("//div[@class='photos']")
                                    .SelectNodes("./a")
                                    .SelectMany(a => a.SelectNodes(".//img"))
                                    .Select(img => img.GetSrc().Split("_")[0] + ".jpg");
                images.AddRange(photos.Select(photo => (StringImageLinkWrapper)photo));
    
                var nextButton = soup.SelectSingleNode("//a[@class='next']");
                if (nextButton is null)
                {
                    break;
                }
                
                var nextPage = nextButton.GetNullableHref();
                if (nextPage is not null)
                {
                    soup = await Soupify("https://en.xchina.co" + nextPage);
                }
                else
                {
                    break;
                }
            }
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    private static string GetVideoUrl(HtmlNode soup)
    {
        var video = soup.SelectSingleNode("//video");
        var videoSrc = video.GetSrc();
        if (!videoSrc.StartsWith("blob:"))
        {
            return videoSrc;
        }
    
        var script = video.ParentNode.SelectNodes(".//script")[1].InnerText;
        var url = script.Split("hls.loadSource(\"")[1];
        url = url.Split("\");")[0];
        return url;
    
    }
}
