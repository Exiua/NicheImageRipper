using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class PutMegaParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "putmega";

    public PutMegaParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PutMegaParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for putmega.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        //const int maxRetries = 4;
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//a[@data-text='album-name']").InnerText;
        var images = new List<StringFileLinkWrapper>();
        while (true)
        {
            var imageList = soup.SelectSingleNodeOrThrow("//div[@class='pad-content-listing']")
                                .SelectNodesOrThrow(".//img")
                                .Select(img => (StringFileLinkWrapper)img.GetSrc().Remove(".md"));
            images.AddRange(imageList);
            var nextPage = soup.SelectSingleNode("//li[@class='pagination-next']");
            if (nextPage is null)
            {
                break;
            }
    
            var nextPageUrl = nextPage.SelectSingleNodeOrThrow(".//a").GetNullableHref();
            if (string.IsNullOrEmpty(nextPageUrl))
            {
                break;
            }
            
            soup = await Soupify("https://putmega.com" + nextPageUrl.Replace("&amp;", "&"), delay: 250);
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
