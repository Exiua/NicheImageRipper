using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SexHdParser : HtmlParser
{
    public SexHdParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for sexhd.pics and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='photobig']//h4")
                            .InnerText
                            .Split(":")[1]
                            .Trim();
        var images = soup.SelectNodes("//div[@class='photobig']//div[@class='relativetop']")
                            .Skip(1)
                            .Select(img => $"https://sexhd.pics{img.SelectSingleNode(".//a").GetHref()}")
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
