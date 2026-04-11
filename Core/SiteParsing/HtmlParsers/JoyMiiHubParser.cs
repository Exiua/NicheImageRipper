using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class JoyMiiHubParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "joymiihub";

    public JoyMiiHubParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<JoyMiiHubParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for joymiihub.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        return await GenericHtmlParser("joymiihub");
    }
}
