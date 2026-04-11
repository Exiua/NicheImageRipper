using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BabesInPornParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "babesinporn";

    public BabesInPornParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabesInPornParser>(filenameScheme))
    {
    }
    
    /// <summary>
    ///     Parses the html for babesinporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse()
    {
        return GenericBabesHtmlParser("//h1[@class='blockheader pink center lowercase']", "//div[@class='list gallery']");
    }
}