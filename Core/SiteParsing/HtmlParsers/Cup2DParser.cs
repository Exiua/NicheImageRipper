using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Cup2DParser : HtmlParser
{
    public Cup2DParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for cup2d.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='post-title entry-title']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var node = soup.SelectSingleNodeOrThrow("//div[@class='entry-content gridshow-clearfix']/div")
                       .SelectNodesOrThrow("./*[self::a or self::iframe]");
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
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
