using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using HtmlAgilityPack;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SfmCompileParser : HtmlParser
{
    public SfmCompileParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for sfmcompile.club and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='g1-alpha g1-alpha-2nd page-title archive-title']")
                            .InnerText
                            .Replace("\"", "");
        var elements = new List<HtmlNode>();
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var items = soup.SelectSingleNode("//ul[@class='g1-collection-items']")
                            .SelectNodes(".//li[@class='g1-collection-item']");
            elements.AddRange(items);
            var nextPage = soup.SelectSingleNode("//a[@class='g1-link g1-link-m g1-link-right next']");
            if (nextPage is null)
            {
                break;
            }
            
            var nextPageUrl = nextPage.GetHref();
            soup = await Soupify(nextPageUrl);
        }
        
        foreach (var element in elements)
        {
            string videoSrc;
            var media = element.SelectSingleNode(".//video");
            if (media is not null)
            {
                videoSrc = media.SelectSingleNode(".//a").GetHref();
                images.Add(videoSrc);
            }
            else
            {
                var videoLink = element.SelectSingleNode(".//a[@class='g1-frame']").GetHref();
                soup = await Soupify(videoLink);
                videoSrc = soup.SelectSingleNode("//video")
                                    .SelectSingleNode(".//source")
                                    .GetSrc();
            }
            images.Add(videoSrc);
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
