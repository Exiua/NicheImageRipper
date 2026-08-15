using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.InfluencersGoneWild;
public class InfluencersGoneWildParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "influencersgonewild";
    public static string[] SupportedUrls => ["https://influencersgonewild.com/"];

    public InfluencersGoneWildParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<InfluencersGoneWildParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for influencersgonewild.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs { ScrollBy = true, Increment = 625, ScrollPauseTime = 1000 }, cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='g1-mega g1-mega-1st entry-title']").InnerText;
        var posts = soup.SelectSingleNodeOrThrow("//div[@class='g1-content-narrow g1-typography-xl entry-content']").SelectNodesOrThrow(".//img|.//video");
        var images = new List<StringFileLinkWrapper>();
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
                    images.Add(post.SelectSingleNodeOrThrow(".//source").GetSrc()); // Unable to actually download videos
                    break;
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}