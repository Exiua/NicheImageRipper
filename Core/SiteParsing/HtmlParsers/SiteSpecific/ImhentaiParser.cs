using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class ImhentaiParser : HtmlParser<ImhentaiParser>, IHtmlParser
{
    public static string ParserName => "imhentai";

    public ImhentaiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ImhentaiParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for imhentai.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        if (!CurrentUrl.Contains("/gallery/"))
        {
            var galCode = CurrentUrl.Split("/")[4];
            CurrentUrl = $"https://imhentai.xxx/gallery/{galCode}/";
        }
    
        var soup = await Soupify();
        var imageContainer = soup.SelectSingleNode("//img[@class='lazy filtered entered loaded']") ?? soup.SelectSingleNodeOrThrow("//img[@class='lazy entered loaded']");

        var images = imageContainer.GetAttributeValue("data-src");
        var numPages = int.Parse(soup.SelectSingleNodeOrThrow("//li[@class='pages']").InnerText.Split()[1]);
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
    
        return RipInfo.FromGenerateInfo(images, dirName, numPages);
    }
}
