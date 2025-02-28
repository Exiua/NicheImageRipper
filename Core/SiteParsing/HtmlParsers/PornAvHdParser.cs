using Core.DataStructures;
using Core.DataStructures.VideoCapturers;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PornAvHdParser : HtmlParser
{
    public PornAvHdParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for pornavhd.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await SolveParseAddCookies();
        var dirName = soup.SelectSingleNode("//h1[@itemprop='name']").InnerText;
        var iframe = soup.SelectSingleNode("//div[@class='responsive-player']/iframe");
        var iframeUrl = iframe.GetSrc();
        var (capturer, _) = await ConfigureNetworkCapture<SexBjCamVideoCapturer>();
        CurrentUrl = iframeUrl;
        var referer = iframeUrl.Split("/")[..3].Join("/") + '/';
        StringImageLinkWrapper playlist;
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                continue;
            }
    
            playlist = new ImageLink(links[0], FilenameScheme, 0)
            {
                Referer = referer
            };
            break;
        }
        
        return new RipInfo([ playlist ], dirName, FilenameScheme);
    }
}
