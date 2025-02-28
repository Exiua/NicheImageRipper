using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HotGirlParser : HtmlParser
{
    public HotGirlParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hotgirl.asia and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        if (!CurrentUrl.Contains("stype=slideshow"))
        {
            var urlParts = CurrentUrl.Split("/")[..4];
            CurrentUrl = "/".Join(urlParts) + "/?stype=slideshow";
        }
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h3[@itemprop='name']").InnerText;
        var images = soup.SelectNodes("//img[@class='center-block w-100']")
                            .Select(image => image.GetSrc())
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();
        
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
