using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HentaiRoxParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hentairox";

    public HentaiRoxParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HentaiRoxParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hentairox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='col-md-7 col-sm-7 col-lg-8 right_details']")
                            .SelectSingleNodeOrThrow(".//h1")
                            .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@id='append_thumbs']")
                            .SelectSingleNodeOrThrow(".//img[@class='lazy preloader']")
                            .GetAttributeValue("data-src");
        var numFiles = int.Parse(soup.SelectSingleNodeOrThrow("//li[@class='pages']").InnerText.Split()[0]);

        return RipInfo.FromGenerateInfo(images, dirName, numFiles);
    }
}
