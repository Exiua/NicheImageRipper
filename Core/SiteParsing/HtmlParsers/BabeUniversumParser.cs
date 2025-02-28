using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BabeUniversumParser : HtmlParser
{
    public BabeUniversumParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Parses the html for babeuniversum.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='title']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@class='three-column']")
                         .SelectNodes(".//div[@class='thumbnail']")
                         .Select(img => Protocol + img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
}