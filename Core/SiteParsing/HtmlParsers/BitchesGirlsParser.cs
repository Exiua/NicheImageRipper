using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BitchesGirlsParser : HtmlParser
{
    public BitchesGirlsParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Parses the html for bitchesgirls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='album-name']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var baseUrl = CurrentUrl;
        if (baseUrl[^1] != '/')
        {
            baseUrl += "/";
        }
        
        var page = 1;
        while (true)
        {
            if (page != 1)
            {
                soup = await Soupify($"{baseUrl}{page}");
            }

            var posts = soup.SelectSingleNode("//div[@class='albumgrid']")
                            .SelectNodes("./a[@class='post-container']")
                            .Select(post => post.GetHref())
                            .ToStringImageLinkWrapperList();
            images.AddRange(posts);
            var loadBtn = soup.SelectSingleNode("//a[@id='loadMore']");
            if (loadBtn is not null)
            {
                page += 1;
            }
            else
            {
                break;
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
}