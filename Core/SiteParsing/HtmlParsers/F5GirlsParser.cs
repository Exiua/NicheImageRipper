using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class F5GirlsParser : HtmlParser
{
    public F5GirlsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for f5girls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectNodesOrThrow("//div[@class='container']")[2]
                            .SelectSingleNodeOrThrow(".//h1")
                            .InnerText;
        var images = new List<StringImageLinkWrapper>();
        var currUrl = CurrentUrl.Replace("?page=1", "");
        var pages = soup.SelectSingleNodeOrThrow("//ul[@class='pagination']")
                        .SelectNodesOrThrow(".//li")
                        .Count - 1;
        for (var i = 0; i < pages; i++)
        {
            var imageList = soup.SelectNodesOrThrow("//img[@class='album-image lazy']")
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
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
