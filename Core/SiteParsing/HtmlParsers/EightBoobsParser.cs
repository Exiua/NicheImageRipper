using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EightBoobsParser : HtmlParser
{
    public EightBoobsParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for 8boobs.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@id='content']")
                            .SelectNodes(".//div[@class='title']")[1]
                            .InnerText;
        var images = soup.SelectSingleNode("//div[@class='gallery clear']")
                            .SelectNodes("./a")
                            .Select(img => Protocol + img
                                        .SelectSingleNode(".//img")
                                        .GetSrc()
                                        .Remove("tn_"))
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
