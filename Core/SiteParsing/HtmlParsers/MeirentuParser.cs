using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class MeirentuParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "meirentu";

    public MeirentuParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MeirentuParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for meirentu.cc and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var referer = CurrentUrl.Split("/").Take(5).Join("/").Split("-")[0];
        if (!referer.EndsWith(".html"))
        {
            referer += ".html";
        }
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var counter = 1;
        while (true)
        {
            Logger.Information("Parsing page {Counter}", counter);
            counter++;
            var imgs = soup.SelectNodesOrThrow("//div[@class='content_left']//img")
                           .Select(img => img.GetSrc())
                           .ToStringImageLinks();
            images.AddRange(imgs);
            var nextPageButton = soup.SelectNodesOrThrow("//div[@class='page']/a")[^1];
            var nextPageButtonText = nextPageButton.InnerText.Trim();
            if (nextPageButtonText != "下页")
            {
                break;
            }
            
            var nextPageUrl = nextPageButton.GetHref();
            soup = await Soupify("https://meirentu.cc" + nextPageUrl);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme, referer: referer);
    }
}