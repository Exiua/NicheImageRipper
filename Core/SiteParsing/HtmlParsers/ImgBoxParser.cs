using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ImgBoxParser : HtmlParser
{
    public ImgBoxParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for imgbox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@id='gallery-view']")
                            .SelectSingleNode(".//h1")
                            .InnerText.Split(" - ")[0];
        var images = soup.SelectSingleNode("//div[@id='gallery-view-content']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc().Replace("thumbs2", "images2").Replace("_b", "_o"))
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
