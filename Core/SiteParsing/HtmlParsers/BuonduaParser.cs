using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class BuonduaParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "buondua";

    public BuonduaParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BuonduaParser>(filenameScheme))
    {
    }
    
    /// <summary>
    ///     Parses the html for buondua.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='article-header']")
                          .SelectSingleNodeOrThrow(".//h1")
                          .InnerText;
        var dirNameSplit = dirName.Split("(");
        if (dirName.Contains("pictures") || dirName.Contains("photos"))
        {
            dirName = dirNameSplit[..^1].Join("(");
        }
        
        var pages = soup.SelectSingleNodeOrThrow("//div[@class='pagination-list']")
                        .SelectNodesOrThrow(".//span")
                        .Count;
        var currUrl = CurrentUrl.Replace("?page=1", "");
        
        var images = new List<StringImageLinkWrapper>();
        for (var i = 0; i < pages; i++)
        {
            var imageList = soup.SelectSingleNodeOrThrow("//div[@class='article-fulltext']")
                                .SelectNodesOrThrow(".//img")
                                .Select(img => img.GetSrc())
                                .Select(dummy => (StringImageLinkWrapper)dummy)
                                .ToList();
            images.AddRange(imageList);
            if (i >= pages - 1)
            {
                continue;
            }

            var nextPage = $"{currUrl}?page={i + 2}";
            CurrentUrl = nextPage;
            soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
            {
                ScrollBy = true
            });
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}