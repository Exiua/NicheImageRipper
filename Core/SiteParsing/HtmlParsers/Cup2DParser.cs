using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Cup2DParser : HtmlParser
{
    public Cup2DParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for cup2d.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        var dirName = soup.SelectSingleNode("//h1[@class='post-title entry-title']/a").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var node = soup.SelectSingleNode("//div[@class='entry-content gridshow-clearfix']/div")
                        .SelectNodes("./*[self::a or self::iframe]");
        foreach (var n in node)
        {
            if(n.Name == "a")
            {
                images.Add((StringImageLinkWrapper)n.GetHref());
            }
            else
            {
                images.Add((StringImageLinkWrapper)n.GetSrc().Replace("/embed/", "/file/"));
            }
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
