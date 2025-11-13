using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SexHdParser : HtmlParser
{
    public SexHdParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for sexhd.pics and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='photobig']//h4")
                            .InnerText
                            .Split(":")[1]
                            .Trim();
        var images = soup.SelectNodesOrThrow("//div[@class='photobig']//div[@class='relativetop']")
                            .Skip(1)
                            .Select(img => $"https://sexhd.pics{img.SelectSingleNodeOrThrow(".//a").GetHref()}")
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
