using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class MyHentaiGalleryParser : HtmlParser
{
    public MyHentaiGalleryParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for myhentaigallery.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='comic-description']")
                            .SelectSingleNode(".//h1")
                            .InnerText;
        var images = soup.SelectSingleNode("//ul[@class='comics-grid clear']")
                            .SelectNodes("./li")
                            .Select(img => img.SelectSingleNode(".//img")
                                                .GetSrc()
                                                .Replace("/thumbnail/", "/original/"))
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
