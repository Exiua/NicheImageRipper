using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SexyKittenPornParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "sexykittenporn";

    public SexyKittenPornParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for sexykittenporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='blockheader']").InnerText;
        var tagList = soup.SelectNodesOrThrow("//div[@class='list gallery col3']")
                            .SelectMany(tag => tag.SelectNodesOrThrow(".//div[@class='item']"));
        var imageLink = tagList.Select(image => 
            $"https://www.sexykittenporn.com{image.SelectSingleNodeOrThrow(".//a").GetHref()}");
        var images = new List<StringImageLinkWrapper>();
        foreach (var link in imageLink)
        {
            soup = await Soupify(link);
            images.Add($"https:{soup.SelectSingleNodeOrThrow("//div[@class='image-wrapper']//img")
                                .GetSrc()}");
        }
        
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
