using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class FaponicParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "faponic";

    public FaponicParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<FaponicParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for faponic.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            ScrollPauseTime = 1000
        });
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='author-content']")
                            .SelectSingleNodeOrThrow(".//a")
                            .InnerText;
        var posts = soup.SelectSingleNodeOrThrow("//div[@id='content']")
                        .SelectNodesOrThrow(".//div[@class='photo-item col-4-width']");
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in posts)
        {
            var video = post.SelectSingleNode(".//a[@class='play-video2']");
            if (video is not null)
            {
                images.Add($"video:{video.GetHref()}");
            }
            else
            {
                images.Add(post.SelectSingleNodeOrThrow(".//img").GetSrc());
            }
        }
        
        foreach(var (i, img) in images.Enumerate())
        {
            if (!img.StartsWith("video:"))
            {
                continue;
            }
    
            soup = await Soupify(((string)img).Remove("video:"));
            var vid = soup.SelectSingleNodeOrThrow("//source").GetSrc();
            images[i] = vid;
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
