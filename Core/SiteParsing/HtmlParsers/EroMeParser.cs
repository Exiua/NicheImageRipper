using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EroMeParser : HtmlParser
{
    public EroMeParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for erome.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            Increment = 1250,
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNode("//h1").InnerText;
        var posts = soup
                    .SelectNodes("//div[@class='col-sm-12 page-content']")[1]
                    .SelectNodes("./div");
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in posts)
        {
            var img = post.SelectSingleNode(".//img");
            if (img is not null)
            {
                var url = img.GetSrc();
                images.Add(url);
                continue;
            }
    
            var vid = post.SelectSingleNode(".//video");
            if (vid is not null)
            {
                var url = vid.SelectSingleNode(".//source").GetSrc();
                images.Add(url);
            }
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
