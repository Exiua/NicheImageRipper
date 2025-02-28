using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BabesMachineParser : HtmlParser
{
    public BabesMachineParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Parses the html for babesmachine.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var gallery = soup.SelectSingleNode("//div[@id='gallery']");
        var dirName = gallery.SelectSingleNode(".//h2")
                             .SelectSingleNode(".//a")
                             .InnerText;
        var images = gallery.SelectSingleNode(".//table").SelectNodes(".//tr")
                            .Select(img => img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                            .Select(img => Protocol + img)
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
}