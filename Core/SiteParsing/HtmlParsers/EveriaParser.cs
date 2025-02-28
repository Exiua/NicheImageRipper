using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EveriaParser : HtmlParser
{
    public EveriaParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for everia.club and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNode("//h1[@class='single-post-title entry-title']").InnerText;
        var images = soup.SelectSingleNode("//figure[@class='wp-block-gallery has-nested-images " +
                                        "columns-1 wp-block-gallery-3 is-layout-flex wp-block-gallery-is-layout-flex']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc())
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
