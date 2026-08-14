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
public class ToonilyParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "toonily";
    public static string[] SupportedUrls => ["https://toonily.me/", "https://toonily.com/"];

    public ToonilyParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ToonilyParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for toonily.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNode("//div[@class='post-title']/h1")!.InnerText;
        var chapterList = soup.SelectSingleNode("//ul[@class='main version-chap no-volumn']")!.SelectNodes("./li")!.Select(li =>
        {
            var a = li.SelectSingleNode("./a");
            var href = a!.GetHref();
            var text = a!.InnerText.Trim();
            return (href, text);
        }).Reverse();
        var images = new List<StringFileLinkWrapper>();
        foreach (var(chapter, name)in chapterList)
        {
            Logger.Information("Parsing {ChapterName}", name);
            soup = await Soupify(chapter, lazyLoadArgs: new LazyLoadArgs { ScrollBy = true, Increment = 5000, ScrollPauseTime = 1000 }, cancellationToken: cancellationToken);
            var imageList = soup.SelectSingleNode("//div[@class='reading-content']")!.SelectNodes("./div")!.Select(div => div.SelectSingleNode("./img")).Select(img => img!.GetSrc().Trim()).ToStringImageLinks();
            images.AddRange(imageList);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}