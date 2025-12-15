using Common.ExtensionMethods;
using System.Text.RegularExpressions;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class SxChineseGirlz01Parser : HtmlParser
{
    public SxChineseGirlz01Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SxChineseGirlz01Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for sxchinesegirlz01.xyz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
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
                soup = await Soupify($"{baseUrl}{i + 1}/");
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
