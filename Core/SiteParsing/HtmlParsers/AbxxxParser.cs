using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class AbxxxParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "abxxx";

    public AbxxxParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<AbxxxParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for abxxx.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var id = CurrentUrl.Split("/")[4];
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText + $" ({id})";
        var images = new List<StringImageLinkWrapper>();
        var url = soup.SelectSingleNodeOrThrow("//video").GetSrc();
        images.Add("https://abxxx.com" + url);

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}