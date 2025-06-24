using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Three600000Parser : HtmlParser
{
    public Three600000Parser(WebDriver driver, Dictionary<string, string> requestHeaders,
                                   FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,
        requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for 3600000.xyz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='entry-title']").InnerText;
        var images = soup.SelectSingleNode("//div[@class='entry-content']/p")
                         .SelectNodes("./a")
                         .Select(a => a.GetHref())
                         .ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}