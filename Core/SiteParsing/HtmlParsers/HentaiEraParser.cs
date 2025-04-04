using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HentaiEraParser : HtmlParser
{
    public HentaiEraParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                     FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hentaiera.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='row gallery_first']/h1").InnerText;
        var pageCount = soup.SelectSingleNode("//button[@id='pages_btn']").InnerText.Trim().Split(' ')[0].ToInt();
        var imageContainer = soup.SelectSingleNode("//img[@class='lazy filtered entered loaded']") ?? soup.SelectSingleNode("//img[@class='lazy entered loaded']");
        var baseUrl = imageContainer.GetAttributeValue("data-src");

        return RipInfo.GenerateInfo(baseUrl, dirName, pageCount);
    }
}