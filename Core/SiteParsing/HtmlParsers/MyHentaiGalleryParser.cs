using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class MyHentaiGalleryParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "myhentaigallery";

    public MyHentaiGalleryParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MyHentaiGalleryParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for myhentaigallery.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='comic-description']")
                            .SelectSingleNodeOrThrow(".//h1")
                            .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//ul[@class='comics-grid clear']")
                            .SelectNodesOrThrow("./li")
                            .Select(img => img.SelectSingleNodeOrThrow(".//img")
                                                .GetSrc()
                                                .Replace("/thumbnail/", "/original/"))
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
