using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class KaiztyParser : HtmlParser
{
    public KaiztyParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for kaizty.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='c-denomination s-denomination']//h2").InnerText;
        var end = dirName.IndexOf(" |", StringComparison.Ordinal);
        const int start = 15; // Length of "Kaizty Photos: "
        dirName = end < 0 ? dirName[start..] : dirName[start..end];
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var imgs = soup.SelectSingleNode("//div[@class='contentme']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc().Split("?")[0])
                            .Where(link => link.StartsWith("https"))
                            .ToStringImageLinks();
            images.AddRange(imgs);
    
            var pagination = soup.SelectSingleNode("//ul[@class='pagination-site']");
            var nextPage = pagination?.SelectNodes(".//a")
                                        .Where(a => a.InnerText.StartsWith("Next"))
                                        .Select(a => a.GetHref())
                                        .FirstOrDefault();
            if (nextPage is null)
            {
                break;
            }
            
            soup = await Soupify($"https://www.kaizty.com{nextPage}");
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
