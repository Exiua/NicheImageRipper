using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class AHottieParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "ahottie";

    public AHottieParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<AHottieParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for ahottie.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='text-xl font-bold pl-3 text-yellow-500']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var imgs = soup.SelectSingleNodeOrThrow("//div[@id='main']/div[@class='my-2']")
                           .SelectNodesOrThrow("./img")
                           .Select(img => img.GetSrc())
                           .ToStringImageLinks();
            images.AddRange(imgs);
            var selector =
                soup.SelectSingleNode("//span[@class='relative z-0 inline-flex flex-wrap shadow-sm rounded-md']");
            if (selector is null)
            {
                break;
            }
            
            var nextButton = selector.SelectNodesOrThrow("./a")[^1];
            if (!nextButton.GetAttributeValue("aria-label").StartsWith("Next"))
            {
                break;
            }
            
            var nextUrl = nextButton.GetHref();
            soup = await Soupify(nextUrl);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}