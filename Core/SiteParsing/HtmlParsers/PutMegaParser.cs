using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PutMegaParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "putmega";

    public PutMegaParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for putmega.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        //const int maxRetries = 4;
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//a[@data-text='album-name']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var imageList = soup.SelectSingleNodeOrThrow("//div[@class='pad-content-listing']")
                                .SelectNodesOrThrow(".//img")
                                .Select(img => (StringImageLinkWrapper)img.GetSrc().Remove(".md"));
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
