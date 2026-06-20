using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class HentaiFoxParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hentaifox";

    public HentaiFoxParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HentaiFoxParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hentaifox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='info']/h1").InnerText;
        var pageCount = soup.SelectSingleNodeOrThrow("//span[@class='i_text pages']").InnerText.Split(" ")[^1].ParseInt();
        var baseImage = soup.SelectSingleNodeOrThrow("//div[@class='g_thumb']//img").GetSrc();

        return RipInfo.FromGenerateInfo(baseImage, dirName, pageCount);
    }
}