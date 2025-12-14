using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SpaceMissParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "spacemiss";

    public SpaceMissParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for spacemiss.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='tdb-title-text']").InnerText;
        var images = soup
                    .SelectSingleNodeOrThrow(
                            "//figure[@class='wp-block-gallery has-nested-images columns-2 is-cropped td-modal-on-gallery wp-block-gallery-1 is-layout-flex wp-block-gallery-is-layout-flex']")
                    .SelectNodesOrThrow(".//a")
                    .Select(img => img.GetHref())
                    .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
