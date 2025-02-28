using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class GyrlsParser : HtmlParser
{
    public GyrlsParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for gyrls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='single_title']").InnerText;
        var images = soup
                        .SelectNodes("//div[@id='gallery-1']//a")
                        .Select(img => img.GetHref())
                        .Select(dummy => (StringImageLinkWrapper)dummy)
                        .ToList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
