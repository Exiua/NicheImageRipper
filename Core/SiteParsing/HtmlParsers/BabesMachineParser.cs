using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BabesMachineParser : HtmlParser
{
    public BabesMachineParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Parses the html for babesmachine.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var gallery = soup.SelectSingleNodeOrThrow("//div[@id='gallery']");
        var dirName = gallery.SelectSingleNodeOrThrow(".//h2")
                             .SelectSingleNodeOrThrow(".//a")
                             .InnerText;
        var images = gallery.SelectSingleNodeOrThrow(".//table").SelectNodesOrThrow(".//tr")
                            .Select(img => img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("tn_"))
                            .Select(img => Protocol + img)
                            .ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}