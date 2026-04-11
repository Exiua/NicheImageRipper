using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Avav19Parser : Av19aParser, IHtmlParser
{
    public static string ParserName => "avav19";

    public Avav19Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Avav19Parser>(filenameScheme))
    {
    }
    
    /// <summary>
    ///     Parses the html for avav19.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        return await base.Parse();
    }
}