using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HottyStopParser : HtmlParser
{
    public HottyStopParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hottystop.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var boxLargeContent = soup.SelectSingleNode("//div[@class='content-center content-center-2']");
        string dirName;
        try
        {
            dirName = boxLargeContent.SelectSingleNode(".//h1").InnerText;
        }
        catch (NullReferenceException)
        {
            dirName = boxLargeContent.SelectSingleNode(".//u").InnerText;
        }
    
        var images = soup.SelectSingleNode("//ul[@class='gallery']")
                            .SelectNodes(".//a")
                            .Select(a => a.GetHref())
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
