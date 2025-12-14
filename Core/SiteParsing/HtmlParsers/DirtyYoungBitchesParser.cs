using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class DirtyYoungBitchesParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "dirtyyoungbitches";

    public DirtyYoungBitchesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for dirtyyoungbitches.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse()
    {
        return GenericBabesHtmlParser("//div[@class='title-holder']//h1", 
            "//div[@class='container cont-light']//div[@class='images']//a[@class='thumb']");
    }
}
