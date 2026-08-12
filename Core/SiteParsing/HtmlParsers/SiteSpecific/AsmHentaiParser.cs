using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class AsmHentaiParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "asmhentai";
    public static string[] SupportedUrls => ["https://asmhentai.com/"];

    public AsmHentaiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<AsmHentaiParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for asmhentai.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='info']/h1").InnerText;
        var pages = soup.SelectSingleNodeOrThrow("//div[@class='pages']/h3").InnerText.Split(" ")[^1].ParseInt();
        var baseUrl = soup.SelectSingleNodeOrThrow("//div[@class='preview_thumb']//img").GetSrc().Split("/")[..^1].Join("/");
        var images = new List<StringFileLinkWrapper>();
        for (var i = 0; i < pages; i++)
        {
            var pageUrl = $"{baseUrl}/{i + 1}.jpg";
            images.Add(pageUrl);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}