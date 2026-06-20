using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class EahentaiParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "eahentai";

    public EahentaiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EahentaiParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for eahentai.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(delay: 1000, lazyLoadArgs: new LazyLoadArgs());
        var dirName = soup.SelectSingleNodeOrThrow("//h2").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='gallery']")
                            .SelectNodesOrThrow(".//a")
                            .Select(img => img
                                        .SelectSingleNodeOrThrow(".//img")
                                        .GetSrc()
                                        .Remove("/thumbnail")
                                        .Replace("t.", "."))
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
