using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class F5GirlsParser : HtmlParser
{
    public F5GirlsParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for f5girls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectNodes("//div[@class='container']")[2]
                            .SelectSingleNode(".//h1")
                            .InnerText;
        var images = new List<StringImageLinkWrapper>();
        var currUrl = CurrentUrl.Replace("?page=1", "");
        var pages = soup.SelectSingleNode("//ul[@class='pagination']")
                        .SelectNodes(".//li")
                        .Count - 1;
        for (var i = 0; i < pages; i++)
        {
            var imageList = soup.SelectNodes("//img[@class='album-image lazy']")
                                .Select(img => img.GetSrc())
                                .Select(dummy => (StringImageLinkWrapper)dummy)
                                .ToList();
            images.AddRange(imageList);
            if (i >= pages - 1)
            {
                continue;
            }
    
            var nextPage = $"{currUrl}?page={i + 2}";
            soup = await Soupify(nextPage);
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
