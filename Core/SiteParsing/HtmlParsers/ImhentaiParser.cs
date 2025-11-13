using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ImhentaiParser : HtmlParser
{
    public ImhentaiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for imhentai.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        if (!CurrentUrl.Contains("/gallery/"))
        {
            var galCode = CurrentUrl.Split("/")[4];
            CurrentUrl = $"https://imhentai.xxx/gallery/{galCode}/";
        }
    
        var soup = await Soupify();
        var imageContainer = soup.SelectSingleNodeOrThrow("//img[@class='lazy filtered entered loaded']") ?? soup.SelectSingleNode("//img[@class='lazy entered loaded']");

        var images = imageContainer.GetAttributeValue("data-src");
        var numPages = int.Parse(soup.SelectSingleNodeOrThrow("//li[@class='pages']").InnerText.Split()[1]);
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
    
        return RipInfo.FromGenerateInfo(images, dirName, numPages);
    }
}
