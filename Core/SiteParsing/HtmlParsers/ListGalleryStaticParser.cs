using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

/// <summary>
///     Shared base for sites whose gallery is a <c>//ul[@class='list-gallery static css has-data']</c>,
///     with the title taken from the first image's alt text.
/// </summary>
public abstract class ListGalleryStaticParser : HtmlParser
{
    protected ListGalleryStaticParser(WebDriver driver, ApiClientManager clientManager,
                                      Dictionary<string, string> requestHeaders,
                                      FilenameScheme filenameScheme = FilenameScheme.Original)
        : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    protected sealed override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var imageList = soup.SelectSingleNodeOrThrow("//ul[@class='list-gallery static css has-data']")
                            .SelectNodesOrThrow(".//a");
        var images = imageList.Select(image => image.GetHref())
                              .Select(dummy => (StringFileLinkWrapper)dummy)
                              .ToList();
        var dirName = imageList[0].SelectSingleNodeOrThrow(".//img")
                                  .GetAttributeValue("alt");

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}