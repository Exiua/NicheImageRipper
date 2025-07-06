using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class FoamGirlParser : HtmlParser
{
    public FoamGirlParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for foamgirl.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='item_title']/h1").InnerText.Split('(')[0];
        var images = new List<StringImageLinkWrapper>();
        var pageContainer = soup.SelectSingleNode("//div[@class='nav-links page_imges']/a[@title='Last']") 
                            ?? soup.SelectSingleNode("//div[@class='nav-links page_imges']").SelectNodes("./a")[^2];
        var pageCount = pageContainer.InnerText.ToInt();
        var baseUrl = CurrentUrl;
        for(var i = 0; i < pageCount; i++)
        {
            var imgs = soup.SelectSingleNode("//div[@id='image_div']/p")
                           .SelectNodes("./a")
                           .Select(a => a.GetHref())
                           .ToStringImageLinks();
            images.AddRange(imgs);
            
            if (i != pageCount - 1)
            {
                soup = await Soupify(baseUrl.Replace(".html", $"_{i+2}.html"));
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}