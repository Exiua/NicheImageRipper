using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class ImgBoxParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "imgbox";

    public ImgBoxParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ImgBoxParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for imgbox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@id='gallery-view']")
                            .SelectSingleNodeOrThrow(".//h1")
                            .InnerText.Split(" - ")[0];
        var images = soup.SelectSingleNodeOrThrow("//div[@id='gallery-view-content']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => img.GetSrc().Replace("thumbs2", "images2").Replace("_b", "_o"))
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
