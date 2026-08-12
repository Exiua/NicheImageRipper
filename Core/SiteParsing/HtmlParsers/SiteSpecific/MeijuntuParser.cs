using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class MeijuntuParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "meijuntu";
    public static string[] SupportedUrls => ["https://meijuntu.com/"];

    public MeijuntuParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MeijuntuParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for meijuntu.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='title']").InnerText;
        var pageCount = soup.SelectSingleNodeOrThrow("//div[@id='pages']").SelectNodesOrThrow("./a")[^2].InnerText.ParseInt();
        var baseUrl = CurrentUrl.Remove(".html");
        var images = new List<StringFileLinkWrapper>();
        for (var i = 0; i < pageCount; i++)
        {
            var img = soup.SelectSingleNodeOrThrow("//div[@class='pictures']/img").GetSrc();
            images.Add(img);
            if (i != pageCount - 1)
            {
                soup = await Soupify($"{baseUrl}-{i + 2}.html", cancellationToken: cancellationToken);
            }

            await Sleep(250, cancellationToken);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}