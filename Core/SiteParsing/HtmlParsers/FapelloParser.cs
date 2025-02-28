using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class FapelloParser : HtmlParser
{
    public FapelloParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for fapello.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        var dirName = soup.SelectSingleNode("//h2[@class='font-semibold lg:text-2xl text-lg mb-2 mt-4']").InnerText;
        var images = soup.SelectSingleNode("//div[@id='content']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc().Replace("_300px", ""))
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
