using System.Text.RegularExpressions;
using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public partial class SxChineseGirlz01Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "sxchinesegirlz01";

    public SxChineseGirlz01Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SxChineseGirlz01Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for sxchinesegirlz01.xyz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='post-title entry-title']").InnerText;
        var numPages = soup.SelectSingleNodeOrThrow("//div[@class='page-links']")
                            .SelectNodesOrThrow("./a")
                            .Count + 1;
        var images = new List<StringImageLinkWrapper>();
        var baseUrl = CurrentUrl;
        for (var i = 0; i < numPages; i++)
        {
            if (i != 0)
            {
                soup = await Soupify($"{baseUrl}{i + 1}/", cancellationToken: cancellationToken);
            }
    
            var imageList = soup.SelectSingleNodeOrThrow("//div[@class='entry-content gridlane-clearfix']")
                                .SelectNodesOrThrow("./figure[@class='wp-block-image size-large']")
                                .Select(img => img.SelectSingleNodeOrThrow(".//img").GetSrc());
            images.AddRange(imageList.Select(img => SxChineseGirlzRegex().Replace(img, ""))
                                        .Select(imageUrl => (StringImageLinkWrapper)imageUrl));
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
    
    [GeneratedRegex(@"-\d+x\d+")]
    private static partial Regex SxChineseGirlzRegex();
}
