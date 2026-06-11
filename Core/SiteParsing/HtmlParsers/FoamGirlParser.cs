using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class FoamGirlParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "foamgirl";

    public FoamGirlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<FoamGirlParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for foamgirl.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='item_title']/h1").InnerText.Split('(')[0];
        var images = new List<StringImageLinkWrapper>();
        var pageContainer = soup.SelectSingleNode("//div[@class='nav-links page_imges']/a[@title='Last']") 
                            ?? soup.SelectSingleNodeOrThrow("//div[@class='nav-links page_imges']").SelectNodesOrThrow("./a")[^2];
        var pageCount = pageContainer.InnerText.ParseInt();
        var baseUrl = CurrentUrl;
        for(var i = 0; i < pageCount; i++)
        {
            var imgs = soup.SelectSingleNodeOrThrow("//div[@id='image_div']/p")
                           .SelectNodesOrThrow("./a")
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