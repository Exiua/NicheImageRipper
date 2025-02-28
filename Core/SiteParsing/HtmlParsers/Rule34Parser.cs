using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Rule34Parser : HtmlParser
{
    public Rule34Parser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for rule34.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    public override Task<RipInfo> Parse()
    {
        return BooruParse(Booru.Rule34);
    }
}
