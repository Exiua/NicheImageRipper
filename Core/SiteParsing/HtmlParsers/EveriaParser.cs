using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EveriaParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "everia";

    public EveriaParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EveriaParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for everia.club and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='single-post-title entry-title']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//figure[@class='wp-block-gallery has-nested-images " +
                                                  "columns-1 wp-block-gallery-3 is-layout-flex wp-block-gallery-is-layout-flex']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => img.GetSrc())
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
