using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PutMegaParser : HtmlParser
{
    public PutMegaParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for putmega.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        //const int maxRetries = 4;
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//a[@data-text='album-name']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var imageList = soup.SelectSingleNode("//div[@class='pad-content-listing']")
                                .SelectNodes(".//img")
                                .Select(img => (StringImageLinkWrapper)img.GetSrc().Remove(".md"));
            images.AddRange(imageList);
            var nextPage = soup.SelectSingleNode("//li[@class='pagination-next']");
            if (nextPage is null)
            {
                break;
            }
    
            var nextPageUrl = nextPage.SelectSingleNode(".//a").GetNullableHref();
            if (string.IsNullOrEmpty(nextPageUrl))
            {
                break;
            }
            
            soup = await Soupify("https://putmega.com" + nextPageUrl.Replace("&amp;", "&"), delay: 250);
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
