using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class CherryNudesParser : HtmlParser
{
    public CherryNudesParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for cherrynudes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//title")
                            .InnerText
                            .Split("-")[0]
                            .Trim();
        var contentUrl = CurrentUrl.Replace("www", "cdn");
        var images = soup.SelectSingleNode("//div[@class='article__gallery-images']")
                            .SelectNodes(".//a")
                            .Select(img => img.GetHref())
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
