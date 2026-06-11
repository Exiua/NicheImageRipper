using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class CuteGirlPornParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "cutegirlporn";

    public CuteGirlPornParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<CuteGirlPornParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for cutegirlporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='gal-title']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//ul[@class='gal-thumbs']")
                         .SelectNodesOrThrow(".//li")
                         .Select(img =>
                              "https://cutegirlporn.com" +
                              img.SelectSingleNodeOrThrow(".//img").GetSrc().Replace("/t", "/"))
                         .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
