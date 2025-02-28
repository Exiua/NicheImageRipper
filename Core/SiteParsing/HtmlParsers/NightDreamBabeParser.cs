using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class NightDreamBabeParser : HtmlParser
{
    public NightDreamBabeParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for nightdreambabe.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override Task<RipInfo> Parse()
    {
        return GenericBabesHtmlParser("//section[@class='outer-section']//h2[@class='section-title title']",
            "//div[@class='lightgallery thumbs quadruple fivefold']//a[@class='gallery-card']");
    }
}
