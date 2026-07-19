using HtmlAgilityPack;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class SfmCompileParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "sfmcompile";

    public SfmCompileParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SfmCompileParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for sfmcompile.club and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='g1-alpha g1-alpha-2nd page-title archive-title']")
                            .InnerText
                            .Replace("\"", "");
        var elements = new List<HtmlNode>();
        var images = new List<StringFileLinkWrapper>();
        while (true)
        {
            var items = soup.SelectSingleNodeOrThrow("//ul[@class='g1-collection-items']")
                            .SelectNodesOrThrow(".//li[@class='g1-collection-item']");
            elements.AddRange(items);
            var nextPage = soup.SelectSingleNode("//a[@class='g1-link g1-link-m g1-link-right next']");
            if (nextPage is null)
            {
                break;
            }
            
            var nextPageUrl = nextPage.GetHref();
            soup = await Soupify(nextPageUrl);
        }
        
        foreach (var element in elements)
        {
            string videoSrc;
            var media = element.SelectSingleNode(".//video");
            if (media is not null)
            {
                videoSrc = media.SelectSingleNodeOrThrow(".//a").GetHref();
                images.Add(videoSrc);
            }
            else
            {
                var videoLink = element.SelectSingleNodeOrThrow(".//a[@class='g1-frame']").GetHref();
                soup = await Soupify(videoLink);
                videoSrc = soup.SelectSingleNodeOrThrow("//video")
                                    .SelectSingleNodeOrThrow(".//source")
                                    .GetSrc();
            }
            images.Add(videoSrc);
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
