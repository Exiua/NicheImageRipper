using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HotStunnersParser : HtmlParser
{
    public HotStunnersParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hotstunners.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='title_content']")
                            .SelectSingleNode(".//h2")
                            .InnerText;
        var images = soup.SelectSingleNode("//div[@class='gallery_janna2']")
                            .SelectNodes(".//img")
                            .Select(img => Protocol + img.GetSrc().Remove("tn_"))
                            .ToStringImageLinkWrapperList();
        
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
