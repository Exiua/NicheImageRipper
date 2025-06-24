using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BaobuaParser : HtmlParser
{
    public BaobuaParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for baobua.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//span[@itemprop='name']").InnerText.Split("|")[0].Trim();
        var images = new List<StringImageLinkWrapper>();
        var pageCount = soup.SelectSingleNode("//div[@class='nav-links']")?.LastChild.InnerText.ToInt() ?? 1;
        var baseUrl = CurrentUrl;
        for(var i = 0; i < pageCount; i++)
        {
            var imgs = soup.SelectSingleNode("//div[@class='entry-content read-details']")
                           .SelectNodes("./figure")
                           .Select(figure => figure.SelectSingleNode("./a").GetHref())
                           .ToStringImageLinks();
            images.AddRange(imgs);
            if (i != pageCount - 1)
            {
                soup = await Soupify($"{baseUrl}?p={i + 2}");
            }
        }
        
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}