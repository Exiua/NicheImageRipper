using Core.DataStructures;
using Core.Enums;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BabesBangParser : HtmlParser
{
    public BabesBangParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Parses the html for babesbang.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override Task<RipInfo> Parse()
    {
        return GenericBabesHtmlParser("//div[@class='main-title']", "//div[@class='gal-block']");
    }
}