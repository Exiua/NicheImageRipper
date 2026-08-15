using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Patreon;
public class PatreonParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "patreon";
    public static string[] SupportedUrls { get; } = ["https://www.patreon.com"];

    public PatreonParser(WebDriver driver, ApiClientManager apiClientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PatreonParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var sessionIds = Config.Cookies.GetValueOrDefault(ParserName, []);
        if (sessionIds.Length == 0)
        {
            throw new RipperException("No session found for " + ParserName);
        }
        
        Driver.SetCookie("session_id", sessionIds[0]);
        var postUrl = ParseUrl(CurrentUrl);
        CurrentUrl = postUrl;
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("").InnerText;
        var images = new List<StringFileLinkWrapper>();
        while (true)
        {
            await LazyLoad(cancellationToken: cancellationToken);
            var button = Driver.TryFindElement(By.XPath("//button[.//div[normalize-space(.)='Load more']]"));
            if (button is null)
            {
                break;
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private static string ParseUrl(string url)
    {
        if (url.Contains("patreon.com"))
        {
            var creator = url.Split('/')[4];
            return $"https://www.patreon.com/cw/{creator}/posts";
        }
        else
        {
            return url;
        }
    }
}