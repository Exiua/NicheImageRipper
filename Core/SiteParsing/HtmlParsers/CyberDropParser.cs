using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.Utility;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class CyberDropParser : ParameterizedHtmlParser
{
    private const int ParseDelay = 500;
    
    public CyberDropParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for cyberdrop.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(string url)
    {
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
                var link = await GetFileUrl(image);
                images.Add(link);
            }
        }
        else if (CurrentUrl.Contains("/f/"))
        {
            var link = await GetFileUrl(CurrentUrl);
            images.Add(link);
        }
        else if (CurrentUrl.Contains("/e/"))
        {
            var video = soup.SelectSingleNode("//video[@id='player']");
            if (video is null)
            {
                await Task.Delay(ParseDelay);
                soup = await Soupify(delay: ParseDelay, xpath: "//video[@id='player']");
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
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
    
    private async Task<string> GetFileUrl(string url)
    {
        Log.Debug("Parsing image: {Image}", url);
        while (true)
        {
            try
            {
                var soup = await Soupify(url, delay: ParseDelay * 2, xpath: "//a[@id='downloadBtn']", xpathTimout: 120);
                #if DEBUG
                Driver.TakeDebugScreenshot();
                Log.Debug("Current url: {CurrentUrl}", Driver.Url);
                #endif
                var link = soup.SelectSingleNode("//a[@id='downloadBtn']")
                               .GetHref();
        
                return link;
            }
            catch (AttributeNotFoundException)
            {
                Log.Debug("Unable to find download button href");
                await Task.Delay(ParseDelay * 2);
            }
        }
    }
}
