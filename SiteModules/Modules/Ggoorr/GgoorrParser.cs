using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Ggoorr;

public class GgoorrParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "ggoorr";
    public static string[] SupportedUrls => ["https://ggoorr.net/"];

    public GgoorrParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<GgoorrParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for ggoorr.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        const string schema = "https://cdn.ggoorr.net";
        var soup = await Soupify(cancellationToken: cancellationToken);
        var id = CurrentUrl.Split("/")[4];
        var dirName = $"[ggoorr]{soup.SelectSingleNodeOrThrow("//h1//a").InnerText} ({id})";
        var posts = soup.SelectSingleNodeOrThrow("//div[@id='article_1']").SelectSingleNodeOrThrow(".//div")
                        .SelectNodesOrThrow(".//img|.//video");
        var images = new List<StringFileLinkWrapper>();
        foreach (var post in posts)
        {
            var link = post.GetNullableSrc();
            if (string.IsNullOrEmpty(link))
            {
                link = post.SelectSingleNodeOrThrow(".//source").GetSrc();
            }

            if (!link.Contains("https://"))
            {
                link = $"{schema}{link}";
            }

            images.Add(link);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}