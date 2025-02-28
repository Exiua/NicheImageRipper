using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class GirlsReleasedParser : HtmlParser
{
    public GirlsReleasedParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for girlsreleased.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(delay: 5000);
        var metadata = soup.SelectNodes("//a[@class='separate']");
        var siteName = metadata[0].InnerText.Split(".")[0].ToTitle();
        var modelName = metadata[1].InnerText;
        var setName = metadata[2].InnerText;
        var dirName = $"{{{siteName}}} {setName} [{modelName}]";
        var images = soup
                        .SelectSingleNode("//div[@class='images']")
                        .SelectNodes(".//img")
                        .Select(img => img.GetSrc().Replace("/t/", "/i/").Replace("t.imx", "i.imx"))
                        .Select(dummy => (StringImageLinkWrapper)dummy)
                        .ToList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
