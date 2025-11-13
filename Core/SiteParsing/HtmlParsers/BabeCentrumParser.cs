using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BabeCentrumParser : HtmlParser
{
    public BabeCentrumParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Parses the html for babecentrum.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='pageHeading']")
                          .SelectNodesOrThrow(".//cufontext")
                          .Select(w => w.InnerText)
                          .Join(" ")
                          .Trim();
        var images = soup.SelectSingleNodeOrThrow("//table")
                         .SelectNodesOrThrow(".//img")
                         .Select(img => Protocol + img.GetAttributeValue("src", "").Remove("tn_"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)   
                         .ToList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}