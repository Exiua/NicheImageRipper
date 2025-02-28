using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EighteenKamiParser : HtmlParser
{
    public EighteenKamiParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for 18kami.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var url = CurrentUrl.Split("/")[..5].Join("/").Replace("/album/", "/photo/");
        var soup = await Soupify(url, lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        var dirName = soup.SelectSingleNode("//div[@class='panel-heading']/div[@class='pull-left']").InnerText;
        var images = soup.SelectSingleNode("//div[@class='row thumb-overlay-albums']")
                            .SelectNodes(".//img")
                            .Select(img => $"https://18kami.com{img.GetSrc()}")
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
