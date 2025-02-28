using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HentaiClubParser : HtmlParser
{
    public HentaiClubParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hentaiclub.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        await LazyLoad(new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//span[@class='post-info-text']").InnerText;
        var images = soup.SelectSingleNode("//div[@id='masonry']")
                            .SelectNodes("./div")
                            .Select(div => div.SelectSingleNode("./img").GetSrc())
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
