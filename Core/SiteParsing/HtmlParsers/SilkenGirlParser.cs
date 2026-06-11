using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SilkenGirlParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "silkengirl";

    public SilkenGirlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SilkenGirlParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for silkengirl.com and silkengirl.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return GenericBabesHtmlParser("//h1[@class='title']|//div[@class='content_main']//h2",
            "//div[@class='thumb_box']");
    }
}
