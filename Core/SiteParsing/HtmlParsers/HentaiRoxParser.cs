using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HentaiRoxParser : HtmlParser
{
    public HentaiRoxParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hentairox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='col-md-7 col-sm-7 col-lg-8 right_details']")
                            .SelectSingleNode(".//h1")
                            .InnerText;
        var images = soup.SelectSingleNode("//div[@id='append_thumbs']")
                            .SelectSingleNode(".//img[@class='lazy preloader']")
                            .GetAttributeValue("data-src");
        var numFiles = int.Parse(soup.SelectSingleNode("//li[@class='pages']").InnerText.Split()[0]);
    
        return new RipInfo([ images ], dirName, generate: true, numUrls: numFiles);
    }
}
