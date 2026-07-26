using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class EightMusesParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "8muses";

    public EightMusesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EightMusesParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for 8muses.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(xpath: "//div[@class='gallery']", lazyLoadArgs: new LazyLoadArgs());
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='top-menu-breadcrumb']")
                            .SelectNodesOrThrow(".//a")
                            .Last()
                            .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='gallery']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => "https://comics.8muses.com" + img.GetSrc().Replace("/th/", "/fm/"))
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
