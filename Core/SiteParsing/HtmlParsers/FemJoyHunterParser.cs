using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class FemJoyHunterParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "femjoyhunter";

    public FemJoyHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<FemJoyHunterParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for femjoyhunter.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse()
    {
        return GenericHtmlParser("femjoyhunter");
    }
}
