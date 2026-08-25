

using OpenQA.Selenium;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Pornzog;
public class PornzogParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "pornzog";
    public static string[] SupportedUrls => ["https://pornzog.com/"];

    public PornzogParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PornzogParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for pornzog.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var id = CurrentUrl.Split("/")[4];
        var dirName = Driver.FindElement(By.XPath("//h2[@class='title video-title']")).Text + $" ({id})";
        var iframe = Driver.FindElement(By.XPath("//iframe[@allowfullscreen]"));
        var iframeSrc = iframe.GetAttribute("src")!;
        var baseUrl = iframeSrc.Split("/").Take(3).Join("/");
        Driver.SwitchTo().Frame(iframe);
        var soup = await Soupify(cancellationToken: cancellationToken);
        var images = new List<StringFileLinkWrapper>();
        var url = soup.SelectSingleNodeOrThrow("//video[@class='jw-video jw-reset']").GetSrc();
        images.Add(baseUrl + url);
        return RipInfo.FromUrlList(images, dirName, FilenameScheme, referer: null);
    }
}