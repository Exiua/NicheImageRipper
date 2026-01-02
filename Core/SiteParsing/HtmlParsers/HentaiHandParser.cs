using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HentaiHandParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hentaihand";

    public HentaiHandParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HentaiHandParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hentaihand.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true
        };
        var soup = await Soupify(xpath: "//div[@class='gallery-image col-6 col-md-3 py-3']//img", lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNodeOrThrow("//h5[@class='comic-title font-weight-bold mb-2']").InnerText;
        var pageCount = soup.SelectSingleNodeOrThrow("//h6[@class='box-header mb-2']")
                            .InnerText
                            .Split('(')[1]
                            .Split(')')[0]
                            .ParseInt();
        var baseUrl = soup.SelectSingleNodeOrThrow("//div[@class='gallery-image col-6 col-md-3 py-3']//img")
                          .GetSrc()
                          .Replace("/thumbnails/", "/images/");

        return RipInfo.FromGenerateInfo(baseUrl, dirName, pageCount);
    }
}