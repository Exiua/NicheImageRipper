using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HundredBucksBabesParser : HtmlParser
{
    public HundredBucksBabesParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for 100bucksbabes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='main-col-2']")
                            .SelectSingleNode(".//h2[@class='heading']")
                            .InnerText;
        var images = soup.SelectSingleNode("//div[@class='main-thumbs']")
                            .SelectNodes(".//img")
                            .Select(img => Protocol + img.GetAttributeValue("data-url"))
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
