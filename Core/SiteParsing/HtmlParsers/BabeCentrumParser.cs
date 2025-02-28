using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BabeCentrumParser : HtmlParser
{
    public BabeCentrumParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Parses the html for babecentrum.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='pageHeading']")
                          .SelectNodes(".//cufontext")
                          .Select(w => w.InnerText)
                          .Join(" ")
                          .Trim();
        var images = soup.SelectSingleNode("//table")
                         .SelectNodes(".//img")
                         .Select(img => Protocol + img.GetAttributeValue("src", "").Remove("tn_"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)   
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
}