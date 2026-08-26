using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Driver;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;


namespace NicheImageRipper.SiteModules.Modules.Generic;

/// <summary>
///     Shared base for sites whose gallery title is <c>//header[@id='top']//h1</c> and whose images live in a
///     <c>list-gallery static css</c>-classed <c>&lt;ul&gt;</c>.
/// </summary>
public abstract class HeaderTitleListGalleryParser : HtmlParser
{
    protected HeaderTitleListGalleryParser(WebDriver driver, 
                                           Dictionary<string, string> requestHeaders,
                                           FilenameScheme filenameScheme = FilenameScheme.Original)
        : base(driver,  requestHeaders, filenameScheme)
    {
    }

    public sealed override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
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