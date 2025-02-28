using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class InfluencersGoneWildParser : HtmlParser
{
    public InfluencersGoneWildParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for influencersgonewild.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 625,
            ScrollPauseTime = 1000
        });
        var dirName = soup.SelectSingleNode("//h1[@class='g1-mega g1-mega-1st entry-title']").InnerText;
        var posts = soup.SelectSingleNode("//div[@class='g1-content-narrow g1-typography-xl entry-content']")
                        .SelectNodes(".//img|.//video");
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in posts)
        {
            switch (post.Name)
            {
                case "img":
                    var src = post.GetSrc();
                    var url = src.Contains(Protocol) ? src : "https://influencersgonewild.com" + src;
                    images.Add(url);
                    break;
                case "video":
                    images.Add(post.SelectSingleNode(".//source").GetSrc()); // Unable to actually download videos
                    break;
            }
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
