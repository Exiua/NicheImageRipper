using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SexyKittenPornParser : HtmlParser
{
    public SexyKittenPornParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for sexykittenporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='blockheader']").InnerText;
        var tagList = soup.SelectNodes("//div[@class='list gallery col3']")
                            .SelectMany(tag => tag.SelectNodes(".//div[@class='item']"));
        var imageLink = tagList.Select(image => 
            $"https://www.sexykittenporn.com{image.SelectSingleNode(".//a").GetHref()}");
        var images = new List<StringImageLinkWrapper>();
        foreach (var link in imageLink)
        {
            soup = await Soupify(link);
            images.Add($"https:{soup.SelectSingleNode("//div[@class='image-wrapper']//img")
                                .GetSrc()}");
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
