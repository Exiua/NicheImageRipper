using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class EveriaParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "everia";

    public EveriaParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EveriaParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for everia.club and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='single-post-title entry-title']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//figure[@class='wp-block-gallery has-nested-images " +
                                                  "columns-1 wp-block-gallery-3 is-layout-flex wp-block-gallery-is-layout-flex']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => img.GetSrc())
                            .Select(dummy => (StringFileLinkWrapper)dummy)
                            .ToList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
