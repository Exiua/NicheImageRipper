using System.Text.RegularExpressions;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class HentaiCosplaysParser : HtmlParser
{
    public HentaiCosplaysParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hentai-cosplays.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        if (CurrentUrl.Contains("/video/"))
        {
            CurrentUrl = CurrentUrl.Replace("hentai-cosplays.com", "porn-video-xxx.com");
            var parser = new PornVideoXXXParser(WebDriver, RequestHeaders, SiteName, FilenameScheme);
            return await parser.Parse();
        }
        
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNode("//div[@id='main_contents']//h2").InnerText;
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var imageList = soup
                            .SelectSingleNode("//div[@id='display_image_detail']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc())
                            .Select(img => HentaiCosplayRegex().Replace(img, ""))
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();
            images.AddRange(imageList);
            var nextPage = soup
                            .SelectSingleNode("//div[@id='paginator']")
                            .SelectNodes(".//span")[^2]
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
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    [GeneratedRegex(@"(/p=\d+)")]
    private static partial Regex HentaiCosplayRegex();
}
