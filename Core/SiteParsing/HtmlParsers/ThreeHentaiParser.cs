using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ThreeHentaiParser : HtmlParser
{
    public ThreeHentaiParser(WebDriver driver, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for 3hentai.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNode("//h1[@class='text-left font-weight-bold']").InnerText;
        var images = soup.SelectSingleNode("//div[@id='thumbnail-gallery']")
                         .SelectNodes("./div")
                         .Select(div => div.SelectSingleNode(".//img").GetSrc())
                         .Select(src => src.Replace("t.", "."))
                         .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
}