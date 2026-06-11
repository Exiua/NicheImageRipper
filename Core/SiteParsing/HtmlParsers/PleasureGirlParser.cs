using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PleasureGirlParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "pleasuregirl";

    public PleasureGirlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PleasureGirlParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for pleasuregirl.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return GenericBabesHtmlParser("//h2[@class='title']",
            "//div[@class='lightgallery-wrap']//div[@class='grid-item thumb']");
    }
}
