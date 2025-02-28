using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class LeakedBbParser : HtmlParser
{
    public LeakedBbParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for leakedbb.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='flow-text left']")
                            .SelectSingleNode(".//h1")
                            .InnerText;
        var imageLinks = soup.SelectSingleNode("//div[@class='post_body scaleimages']")
                            .SelectNodes("./img")
                            .Select(img => img.GetSrc())
                            .ToList();
        var images = new List<StringImageLinkWrapper>();
        foreach (var link in imageLinks)
        {
            if (!link.Contains("postimg.cc"))
            {
                images.Add(link);
                continue;
            }
    
            soup = await Soupify(link);
            var img = soup.SelectSingleNode("//a[@id='download']").GetHref().Split("?")[0];
            images.Add(img);
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
