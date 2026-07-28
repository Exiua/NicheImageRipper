using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

/// <summary>
///     Shared base for sites whose gallery title is <c>//header[@id='top']//h1</c> and whose images live in a
///     <c>list-gallery static css</c>-classed <c>&lt;ul&gt;</c>.
/// </summary>
public abstract class HeaderTitleListGalleryParser : HtmlParser
{
    protected HeaderTitleListGalleryParser(WebDriver driver, ApiClientManager clientManager,
                                           Dictionary<string, string> requestHeaders,
                                           FilenameScheme filenameScheme = FilenameScheme.Original)
        : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    protected sealed override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//header[@id='top']").SelectSingleNodeOrThrow(".//h1").InnerText;
        var images = soup
                    .SelectSingleNodeOrThrow(
                         "//ul[contains(@class, 'list-gallery') and contains(@class, 'static') and contains(@class, 'css')]")
                    .SelectNodesOrThrow(".//a")
                    .Select(img => img.GetHref())
                    .Select(dummy => (StringFileLinkWrapper)dummy)
                    .ToList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}