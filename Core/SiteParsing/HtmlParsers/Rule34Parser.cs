using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Rule34Parser : BooruParser, IHtmlParser
{
    public static string ParserName => "rule34";
    
    public Rule34Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Rule34Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for rule34.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Booru.Rule34);
    }
}
