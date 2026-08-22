using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.ThreeHentai;
public class ThreeHentaiParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "3hentai";
    public static string[] SupportedUrls => ["https://3hentai.net/"];

    public ThreeHentaiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ThreeHentaiParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for 3hentai.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='text-left font-weight-bold']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@id='thumbnail-gallery']").SelectNodesOrThrow("./div").Select(div => div.SelectSingleNodeOrThrow(".//img").GetSrc()).Select(src => src.Replace("t.", ".")).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}