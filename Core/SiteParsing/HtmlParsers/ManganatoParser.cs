using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ManganatoParser : HtmlParser
{
    public ManganatoParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for manganato.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='story-info-right']")
                            .SelectSingleNode(".//h1")
                            .InnerText;
        var nextChapter = soup.SelectSingleNode("//ul[@class='row-content-chapter']")
                            .SelectNodes("./li")[^1]
                            .SelectSingleNode(".//a");
        var images = new List<StringImageLinkWrapper>();
        var counter = 1;
        while (nextChapter is not null)
        {
            Log.Information($"Parsing Chapter {counter}");
            counter += 1;
            soup = await Soupify(nextChapter.GetHref());
            var chapterImages = soup.SelectSingleNode("//div[@class='container-chapter-reader']")
                                    .SelectNodes(".//img");
            images.AddRange(chapterImages.Select(img => (StringImageLinkWrapper)img.GetSrc()));
            nextChapter = soup.SelectSingleNode("//a[@class='navi-change-chapter-btn-next a-h']");
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
