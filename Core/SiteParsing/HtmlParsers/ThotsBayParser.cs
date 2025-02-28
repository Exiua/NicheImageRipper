using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ThotsBayParser : HtmlParser
{
    public ThotsBayParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for thotsbay.tv and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNode("//div[@class='actor-name']/h1").InnerText;
        var container = soup.SelectSingleNode("//div[@id='media-items-all']");
        var items = container.SelectNodes("./div")
                                .Select(div => $"https://thotsbay.tv{div.SelectSingleNode(".//a").GetHref()}");
        var images = new List<StringImageLinkWrapper>();
        foreach (var item in items)
        {
            soup = await Soupify(item, delay: 250);
        }
        
        // Unable to download videos from blob, parse in on hold
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
