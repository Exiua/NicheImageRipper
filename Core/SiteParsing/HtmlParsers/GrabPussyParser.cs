using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class GrabPussyParser : HtmlParser
{
    public GrabPussyParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for grabpussy.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override Task<RipInfo> Parse()
    {
        return GenericBabesHtmlParser("(//div[@class='c-title'])[2]//h1",
            "//div[@class='gal own-gallery-images']/a");
    }
}
