using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ImhentaiParser : HtmlParser
{
    public ImhentaiParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
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
        var images = soup.SelectSingleNode("//img[@class='lazy preloader']").GetAttributeValue("data-src", "");
        if (images == "")
        {
            throw new NotFoundException("Image not found");
        }
    
        var numPages = int.Parse(soup.SelectSingleNode("//li[@class='pages']").InnerText.Split()[1]);
        var dirName = soup.SelectSingleNode("//h1").InnerText;
    
        return new RipInfo([images], dirName, generate: true, numUrls: numPages);
    }
}
