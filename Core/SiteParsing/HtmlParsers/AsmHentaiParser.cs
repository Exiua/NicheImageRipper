using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class AsmHentaiParser : HtmlParser
{
    public AsmHentaiParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                           FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for asmhentai.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='info']/h1").InnerText;
        var pages = soup.SelectSingleNode("//div[@class='pages']/h3").InnerText.Split(" ")[^1].ToInt();
        var baseUrl = soup.SelectSingleNode("//div[@class='preview_thumb']//img").GetSrc().Split("/")[..^1].Join("/");
        var images = new List<StringImageLinkWrapper>();
        for(var i = 0; i < pages; i++)
        {
            var pageUrl = $"{baseUrl}/{i + 1}.jpg";
            images.Add(pageUrl);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}