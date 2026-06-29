using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using Serilog;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class IwaraParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "iwara";

    public IwaraParser(WebDriver driver, ApiClientManager apiClientManager, Dictionary<string, string> requestHeaders,
                       FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<IwaraParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@title]").InnerText;
        var images = new List<StringImageLinkWrapper>();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}