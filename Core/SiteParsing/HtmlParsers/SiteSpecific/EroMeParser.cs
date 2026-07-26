using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class EroMeParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "erome";

    public EroMeParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EroMeParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for erome.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            Increment = 1250,
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
        var posts = soup
                    .SelectNodesOrThrow("//div[@class='col-sm-12 page-content']")[1]
                    .SelectNodesOrThrow("./div");
        var images = new List<StringFileLinkWrapper>();
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
                var url = vid.SelectSingleNodeOrThrow(".//source").GetSrc();
                images.Add(url);
            }
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
