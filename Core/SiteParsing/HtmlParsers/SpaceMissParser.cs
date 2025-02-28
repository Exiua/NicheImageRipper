using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SpaceMissParser : HtmlParser
{
    public SpaceMissParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for spacemiss.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='tdb-title-text']").InnerText;
        var images = soup
                    .SelectSingleNode(
                            "//figure[@class='wp-block-gallery has-nested-images columns-2 is-cropped td-modal-on-gallery wp-block-gallery-1 is-layout-flex wp-block-gallery-is-layout-flex']")
                    .SelectNodes(".//a")
                    .Select(img => img.GetHref())
                    .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
