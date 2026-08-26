using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;

using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.PornVideoXXX;
public class PornVideoXXXParser : HtmlParser
{
    public PornVideoXXXParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the HTML for porn-video-xxx.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='blog-info--title']").InnerText;
        var images = soup.SelectNodesOrThrow("//video/source").Select(source => source.GetSrc()).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}