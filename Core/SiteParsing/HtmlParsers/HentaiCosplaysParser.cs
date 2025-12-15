using Common.ExtensionMethods;
using System.Text.RegularExpressions;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class HentaiCosplaysParser : HtmlParser
{
    public HentaiCosplaysParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HentaiCosplaysParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hentai-cosplays.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        if (CurrentUrl.Contains("/video/"))
        {
            CurrentUrl = CurrentUrl.Replace("hentai-cosplays.com", "porn-video-xxx.com");
            var parser = new PornVideoXXXParser(WebDriver, ApiClientManager, RequestHeaders, FilenameScheme);
            return await parser.ParseSite(CurrentUrl);
        }
        
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNodeOrThrow("//div[@id='main_contents']//h2").InnerText;
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var imageList = soup
                            .SelectSingleNodeOrThrow("//div[@id='display_image_detail']")
                            .SelectNodesSafe(".//img")
                            .Select(img => img.GetSrc())
                            .Select(img => HentaiCosplayRegex().Replace(img, ""))
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();
            images.AddRange(imageList);
            var nextPage = soup
                            .SelectSingleNodeOrThrow("//div[@id='paginator']")
                            .SelectNodesOrThrow(".//span")[^2]
                            .SelectSingleNode(".//a");
            if (nextPage is null)
            {
                break;
            }
    
            soup = await Soupify($"https://hentai-cosplays.com{nextPage.GetHref()}", lazyLoadArgs: new LazyLoadArgs
            {
                ScrollBy = true
            });
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
    
    [GeneratedRegex(@"(/p=\d+)")]
    private static partial Regex HentaiCosplayRegex();
}
