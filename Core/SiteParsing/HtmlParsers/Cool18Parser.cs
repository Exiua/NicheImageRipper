using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Cool18Parser : HtmlParser
{
    public Cool18Parser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var showContent = soup.SelectSingleNode("//td[@class='show_content']");
        var dirName = showContent.SelectSingleNode(".//b").InnerText;
        var images = showContent.SelectSingleNode(".//pre")
                                .SelectNodes(".//img")
                                .Select(img => img.GetSrc())
                                .Select(dummy => (StringImageLinkWrapper)dummy)
                                .ToList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
