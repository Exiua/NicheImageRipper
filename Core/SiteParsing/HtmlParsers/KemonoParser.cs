using Core.DataStructures;
using Core.Enums;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class KemonoParser : DotPartyParser, IHtmlParser
{
    public static string ParserName => "kemono";

    public KemonoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<KemonoParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for kemono.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(string url)
    {
        if (url != "")
        {
            CurrentUrl = url;
        }

        return await DotPartyParse("https://kemono.cr");
    }
}