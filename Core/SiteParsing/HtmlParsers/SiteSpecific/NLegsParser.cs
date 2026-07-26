using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.Utility;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class NLegsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "nlegs";

    public NLegsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NLegsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for nlegs.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        const string domain = "https://www.nlegs.com";
        const int delay = 1000;
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//strong").InnerText;
        var numPages = soup.SelectSingleNodeOrThrow("//ul[@class='pagination pagination']")
                            .SelectNodesOrThrow("./li")
                            .Count;
        var baseUrl = CurrentUrl.Split(".")[..^1].Join(".");
        var images = new List<StringFileLinkWrapper>();
        for (var i = 0; i < numPages; i++)
        {
            Logger.Information("Parsing page {i} of {numPages}", i + 1, numPages);
            var posts = soup.SelectSingleNodeOrThrow("//div[@class='col-md-12 col-xs-12 ']")
                            .SelectNodesOrThrow(".//a")
                            .Select(a => domain + a.GetHref())
                            .ToStringImageLinks();
            images.AddRange(posts);
            soup = await Soupify($"{baseUrl}/{i + 2}.html"); // Pages are 1-indexed
            await Task.Delay(delay);
        }
    
        var cookieJar = Driver.GetCookieJar();
        var cookies = cookieJar.AllCookies
                                .Aggregate("", (current, cookie) => current + $"{cookie.Name}={cookie.Value}; ")
                                .Trim();
        RequestHeaders[RequestHeaderKeys.Cookie] = cookies;
        
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
