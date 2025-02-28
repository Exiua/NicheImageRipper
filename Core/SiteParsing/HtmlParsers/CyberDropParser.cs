using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class CyberDropParser : HtmlParser
{
    public CyberDropParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for cyberdrop.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        return await CyberDropParse("");
    }

    /// <summary>
    ///     Parses the html for cyberdrop.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CyberDropParse(string url)
    {
        const int parseDelay = 500;
        
        if (url != "")
        {
            CurrentUrl = url;
        }
    
        var soup = await SolveParseAddCookies();
        var titleNode = soup.SelectSingleNode("//h1[@id='title']");
        var dirName = titleNode is not null ? titleNode.InnerText : $"[CyberDrop] {CurrentUrl.Split("/")[^1]}";
        
        var images = new List<StringImageLinkWrapper>();
        if (CurrentUrl.Contains("/a/"))
        {
            var imageList = soup.SelectNodes("//div[@class='image-container column']")
                                .Select(image => image
                                                .SelectSingleNode(".//a[@class='image']")
                                                .GetHref())
                                .Select(href => $"https://cyberdrop.me{href}");
            foreach (var image in imageList)
            {
                soup = await Soupify(image, delay: parseDelay, xpath: "//a[@id='downloadBtn']");
                var link = soup
                            .SelectSingleNode("//a[@id='downloadBtn']")
                            .GetHref();
                images.Add(link);
            }
        }
        else if (CurrentUrl.Contains("/f/"))
        {
            var link = soup
                        .SelectSingleNode("//a[@id='downloadBtn']")
                        .GetHref();
            images.Add(link);
        }
        else if (CurrentUrl.Contains("/e/"))
        {
            var video = soup.SelectSingleNode("//video[@id='player']");
            if (video is null)
            {
                await Task.Delay(parseDelay);
                soup = await Soupify(delay: parseDelay, xpath: "//video[@id='player']");
                video = soup.SelectSingleNode("//video[@id='player']");
            }
            
            var link = video.GetVideoSrc();
            images.Add(link);
        }
        else
        {
            Log.Error("Unknown CyberDrop url type: {CurrentUrl}", CurrentUrl);
            throw new RipperException("Unknown CyberDrop url type");
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
