using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.ApComics;
public class ApComicsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "apcomics";
    public static string[] SupportedUrls => ["https://apcomics.org/"];

    public ApComicsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ApComicsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for apcomics.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        await WaitForElement("//ul[@class='main version-chap no-volumn']/li/a", timeout: 60, cancellationToken: cancellationToken);
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='post-title']/h1").InnerText;
        var images = new List<StringFileLinkWrapper>();
        var chapters = soup.SelectSingleNodeOrThrow("//ul[@class='main version-chap no-volumn']").SelectNodesOrThrow("./li").Select(li => li.SelectSingleNodeOrThrow("./a").GetHref()).Reverse();
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true
        };
        foreach (var chapter in chapters)
        {
            Logger.Debug("Parsing chapter {Chapter}", chapter);
            soup = await Soupify(chapter, lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
            var imgs = soup.SelectSingleNodeOrThrow("//div[@class='reading-content']").SelectNodesOrThrow("./div").Select(div => div.SelectSingleNodeOrThrow("./img").GetSrc().Trim()).ToStringImageLinks();
            images.AddRange(imgs);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}