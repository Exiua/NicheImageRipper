using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class WnacgParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "wnacg";

    public WnacgParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<WnacgParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for wnacg.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        if (CurrentUrl.Contains("-slist-"))
        {
            CurrentUrl = CurrentUrl.Replace("-slist-", "-index-");
        }
        
        var soup = await SolveParseAddCookies();
        Log.Debug("Fetching directory name");
        var dirNode = soup.SelectSingleNode("//h2");
        if (dirNode is null)
        {
            Driver.Refresh();
            soup = await Soupify();
            dirNode = soup.SelectSingleNodeOrThrow("//h2");
        }

        var dirName = dirNode.InnerText;
        var numImages = soup.SelectSingleNodeOrThrow("//span[@class='name tb']").InnerText;
        var imageLinks = new List<string>();
        Log.Debug("Fetching image links");
        var page = 1;
        while (true)
        {
            Log.Debug("Fetching page {Page}", page);
            page++;
            var imageList = soup
                            .SelectNodesOrThrow("//li[@class='li tb gallary_item']")
                            .Select(n => n.SelectSingleNodeOrThrow(".//a").GetHref());
            imageLinks.AddRange(imageList);
            var nextPageButton = soup.SelectSingleNode("//span[@class='next']");
            if (nextPageButton is null)
            {
                break;
            }
            
            var nextPageUrl = nextPageButton.SelectSingleNodeOrThrow(".//a").GetHref();
            soup = await Soupify($"https://www.wnacg.com{nextPageUrl}");
        }
        
        Log.Debug("Found {NumImages} images", imageLinks.Count);
        var images = new List<StringImageLinkWrapper>();
        foreach (var image in imageLinks)
        {
            await JitterSleep(max: 350);
            Log.Debug("Fetching image {Image}", image);
            CurrentUrl = $"https://www.wnacg.com{image}";
            soup = await SolveParse();
            var img = soup.SelectSingleNodeOrThrow("//img[@id='picarea']");
            var imgSrc = img.GetSrc();
            images.Add(imgSrc.Contains("https:") ? imgSrc : $"https:{imgSrc}");
        }
        
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
