using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using OpenQA.Selenium;
using HtmlAgilityPack;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class PornComics18Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "porncomics18";
    public static string[] SupportedUrls => ["https://porncomics18.com/"];

    public PornComics18Parser(WebDriver driver, ApiClientManager apiClientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PornComics18Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for porncomics18.com and extracts the relevant information necessary for downloading images from the site
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
        var chapters = soup.SelectSingleNodeOrThrow("//ul[@class='sub-chap-list']").SelectNodesOrThrow("./li").Select(li => li.SelectSingleNodeOrThrow("./a")).Select(a => a.GetHref()).Reverse();
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        };
        var images = new List<StringFileLinkWrapper>();
        foreach (var chapter in chapters)
        {
            Logger.Information("Parsing chapter: {ChapterUrl}", chapter);
            soup = await Soupify(chapter, lazyLoadArgs: lazyLoadArgs, xpath: "//div[@class='reading-content']/img");
            var imgs = soup.SelectSingleNodeOrThrow("//div[@class='reading-content']").SelectNodesOrThrow("./div").Select(div => div.SelectSingleNodeOrThrow("./img")).Select(img => img.GetSrc()).ToStringImageLinks();
            images.AddRange(imgs);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}