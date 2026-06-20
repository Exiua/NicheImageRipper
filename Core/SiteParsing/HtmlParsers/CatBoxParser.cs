using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class CatBoxParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "catbox";

    public CatBoxParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<CatBoxParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for catbox.moe and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        Logger.Warning("Catbox.moe support is experimental and may not work as expected");
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='title']/h1").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='imagecontainer']")
                            .SelectNodesOrThrow("./video")
                            .Select(vid => vid.GetSrc())
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
