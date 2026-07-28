using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

/// <summary>
///     Shared base for sites whose gallery title is inside <c>//img[@title='Click To Enlarge!']</c>'s alt text
///     (truncated at the first " - ") and whose images live in <c>//div[@class='gallery_thumb']</c> thumbnails.
/// </summary>
public abstract class ClickToEnlargeGalleryParser : HtmlParser
{
    protected ClickToEnlargeGalleryParser(WebDriver driver, ApiClientManager clientManager,
                                          Dictionary<string, string> requestHeaders,
                                          FilenameScheme filenameScheme = FilenameScheme.Original)
        : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    protected sealed override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//img[@title='Click To Enlarge!']")
                          .GetAttributeValue("alt")
                          .Split(" ")
                          .TakeWhile(s => s != "-")
                          .Join(" ");
        var images = soup.SelectNodesOrThrow("//div[@class='gallery_thumb']")
                         .Select(img => Protocol + img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("tn_"))
                         .ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}