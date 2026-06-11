using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Three600000Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "3600000";

    public Three600000Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Three600000Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for 3600000.xyz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='entry-title']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='entry-content']/p")
                         .SelectNodesOrThrow("./a")
                         .Select(a => a.GetHref())
                         .ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}