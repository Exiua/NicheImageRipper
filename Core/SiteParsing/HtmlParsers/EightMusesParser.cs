using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EightMusesParser : HtmlParser
{
    public EightMusesParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for 8muses.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(xpath: "//div[@class='gallery']", lazyLoadArgs: new LazyLoadArgs());
        var dirName = soup.SelectSingleNode("//div[@class='top-menu-breadcrumb']")
                            .SelectNodes(".//a")
                            .Last()
                            .InnerText;
        var images = soup.SelectSingleNode("//div[@class='gallery']")
                            .SelectNodes(".//img")
                            .Select(img => "https://comics.8muses.com" + img.GetSrc().Replace("/th/", "/fm/"))
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
