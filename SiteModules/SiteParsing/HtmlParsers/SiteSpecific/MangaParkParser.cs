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
public class MangaParkParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "mangapark";
    public static string[] SupportedUrls => ["https://mangapark.net/"];

    public MangaParkParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MangaParkParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for mangapark.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        List<Dictionary<string, string>> cookies = [new()
        {
            ["name"] = "nsfw",
            ["value"] = "2"
        }

        ];
        var soup = await SolveParseAddCookies(cookies: cookies, cancellationToken: cancellationToken);
        Driver.SetCookie("nsfw", "2");
        ;
        var dirName = soup.SelectSingleNodeOrThrow("//h3[@class='text-lg md:text-2xl font-bold']/a").InnerText;
        var chapterList = soup.SelectSingleNodeOrThrow("//div[@data-name='chapter-list']").SelectNodesOrThrow("./div")[1].SelectSingleNodeOrThrow("./div/div").SelectNodesOrThrow("./div").Select(div => div.SelectSingleNodeOrThrow(".//a").GetHref()).Reverse();
        var images = new List<StringFileLinkWrapper>();
        foreach (var chapter in chapterList)
        {
            var chapterUrl = $"https://mangapark.net{chapter}";
            Logger.Debug("Parsing chapter {ChapterUrl}", chapterUrl);
            soup = await Soupify(chapterUrl, xpath: "//div[@data-name='image-item']", cancellationToken: cancellationToken);
            var pages = soup.SelectNodesOrThrow("//div[@data-name='image-item']").Select(div => div.SelectSingleNodeOrThrow(".//img").GetSrc()).ToStringImageLinks();
            images.AddRange(pages);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}