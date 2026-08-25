using System.Text.RegularExpressions;


using OpenQA.Selenium;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.FourKHd;
public partial class FourKHdParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "4khd";
    public static string[] SupportedUrls => ["https://www.4khd.com/"];

    public FourKHdParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<FourKHdParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for 4khd.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        await LazyLoad(new LazyLoadArgs { StopElement = By.XPath("//ul[@class='page-links']") }, cancellationToken: cancellationToken);
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h3").InnerText;
        var numPages = soup.SelectSingleNode("//ul[@class='page-links']")?.SelectNodes("./li")?.Count ?? 1;
        var baseUrl = CurrentUrl;
        var images = new List<StringFileLinkWrapper>();
        for (var page = 1; page <= numPages; page++)
        {
            Logger.Information("Parsing page {page} of {numPages}", page, numPages);
            var baseElement = soup.SelectSingleNode("//div[@id='basicExample']") ?? soup.SelectSingleNodeOrThrow("//div[@id='basicE']");
            var imgs = baseElement.SelectNodesOrThrow("./a").Select(a => a.GetHref().Split("?")[0]).ToStringFileLinks();
            images.AddRange(imgs);
            // The first page is already loaded
            soup = await Soupify($"{baseUrl}/{page + 1}", lazyLoadArgs: new LazyLoadArgs { StopElement = By.XPath("//ul[@class='page-links']") }, cancellationToken: cancellationToken);
        }

        var baseName = images[0].Split("/")[^1];
        var match = FourKHdRegex().Match(baseName);
        baseName = match.Groups[1].Value;
        images = images.Where(img => img.Contains(baseName)).ToList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    [GeneratedRegex(@"([a-zA-Z0-9-]+)")]
    private static partial Regex FourKHdRegex();
}