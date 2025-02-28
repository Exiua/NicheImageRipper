using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class TheOmegaProjectParser : HtmlParser
{
    public TheOmegaProjectParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for theomegaproject.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectNodes("//h2[@class='section-title title']")[1].InnerText
                            .Split("Porn")[0]
                            .Split("porn")[0]
                            .Trim();
        var images = soup.SelectSingleNode("//div[@class='lightgallery thumbs quadruple fivefold']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc())
                            .ToStringImageLinkWrapperList();
        
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
