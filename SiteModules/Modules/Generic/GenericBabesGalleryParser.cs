using Sdk.Common;
using Sdk.Common.ExtensionMethods;
using Sdk.DataStructures;
using Sdk.Driver;
using Sdk.Enums;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.Generic;

/// <summary>
///     Shared base for "babes"-style gallery sites: title comes from <see cref="DirNameXpath"/>'s inner text,
///     images come from <c>&lt;img&gt;</c> elements nested inside <see cref="ImageContainerXpath"/> matches,
///     with any <c>tn_</c> thumbnail prefix stripped from each image's src.
/// </summary>
public abstract class GenericBabesGalleryParser : HtmlParser
{
    /// <summary>XPath to the single element whose inner text is the gallery/directory name.</summary>
    protected abstract string DirNameXpath { get; }

    /// <summary>XPath to the container element(s) each holding one or more <c>&lt;img&gt;</c> thumbnails.</summary>
    protected abstract string ImageContainerXpath { get; }

    protected GenericBabesGalleryParser(WebDriver driver, ApiClientManager clientManager,
                                        Dictionary<string, string> requestHeaders,
                                        FilenameScheme filenameScheme = FilenameScheme.Original)
        : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    public sealed override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow(DirNameXpath).InnerText;
        var images = soup.SelectNodesOrThrow(ImageContainerXpath)
                         .SelectMany(im => im.SelectNodesOrThrow(".//img"))
                         .Select(img => Protocol + img.GetSrc().Remove("tn_"))
                         .Select(dummy => (StringFileLinkWrapper)dummy)
                         .ToList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}