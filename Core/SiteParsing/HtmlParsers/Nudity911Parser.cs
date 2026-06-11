using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Nudity911Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "nudity911";

    public Nudity911Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Nudity911Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for nudity911.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return GenericBabesHtmlParser("//h1", "//tr[@valign='top']//td[@align='center']//table[@width='650']//td[@width='33%']");
    }
}
