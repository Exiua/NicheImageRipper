using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Cool18Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "cool18";

    public Cool18Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Cool18Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var showContent = soup.SelectSingleNodeOrThrow("//td[@class='show_content']");
        var dirName = showContent.SelectSingleNodeOrThrow(".//b").InnerText;
        var images = showContent.SelectSingleNodeOrThrow(".//pre")
                                .SelectNodesOrThrow(".//img")
                                .Select(img => img.GetSrc())
                                .Select(dummy => (StringImageLinkWrapper)dummy)
                                .ToList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
