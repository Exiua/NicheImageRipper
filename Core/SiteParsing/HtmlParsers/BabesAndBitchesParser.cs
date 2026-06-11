using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BabesAndBitchesParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "babesandbitches";

    public BabesAndBitchesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabesAndBitchesParser>(filenameScheme))
    {
    }
    
    /// <summary>
    ///     Parses the html for babesandbitches.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@id='title']")
                          .InnerText
                          .Split("picture")[0]
                          .Trim();
        var images = soup.SelectNodesOrThrow("//a[@class='gallery-thumb']")
                         .Select(img => Protocol + img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("tn_"))
                         .ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}