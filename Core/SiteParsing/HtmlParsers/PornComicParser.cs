using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class PornComicParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "porncomic";

    public PornComicParser(WebDriver driver, ApiClientManager apiClientManager,
                           Dictionary<string, string> requestHeaders,
                           FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<PornComicParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for porncomic.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        Driver.AddCookie("wpmanga-adault", "1");
        Driver.Refresh();
        var elementName = await WaitForElement("//ul[@class='sub-chap-list']/li/a", timeout: 60);
        if (elementName is null)
        {
            Logger.Warning("No chapters found on the page."); 
        }
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='post-title']/h1").InnerText;
        var chapterContainer = soup.SelectSingleNode("//ul[@class='main version-chap no-volumn']") ?? soup.SelectSingleNodeOrThrow("//ul[@class='sub-chap-list']");
        var chapters = chapterContainer.SelectNodesOrThrow("./li")
                                       .Select(li => li.SelectSingleNodeOrThrow("./a"))
                                       .Select(a => a.GetHref());

        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        };
        var images = new List<StringImageLinkWrapper>();
        foreach (var chapter in chapters)
        {
            Logger.Information("Parsing chapter: {ChapterUrl}", chapter);
            soup = await Soupify(chapter, xpath: "//div[@class='reading-content']/div/img", delay: 250);
            var imgs = soup.SelectSingleNodeOrThrow("//div[@class='reading-content']")
                           .SelectNodesOrThrow("./div")
                           .Select(div => div.SelectSingleNodeOrThrow("./img"))
                           .Select(img => img.GetAttributeValue("data-src").Trim())
                           .ToStringImageLinks();
            images.AddRange(imgs);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}