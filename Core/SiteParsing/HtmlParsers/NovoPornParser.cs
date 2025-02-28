using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class NovoPornParser : HtmlParser
{
    public NovoPornParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for novoporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//section[@class='outer-section']")
                            .SelectSingleNode(".//h2")
                            .InnerText
                            .Split("porn")[0]
                            .Trim();
        var images = soup.SelectNodes("//div[@class='thumb grid-item']")
                            .Select(img => img.SelectSingleNode(".//img").GetSrc().Replace("tn_", ""))
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
