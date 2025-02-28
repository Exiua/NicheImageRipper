using System.Text.RegularExpressions;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class SxChineseGirlz01Parser : HtmlParser
{
    public SxChineseGirlz01Parser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for sxchinesegirlz01.xyz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='post-title entry-title']").InnerText;
        var numPages = soup.SelectSingleNode("//div[@class='page-links']")
                            .SelectNodes("./a")
                            .Count + 1;
        var images = new List<StringImageLinkWrapper>();
        var baseUrl = CurrentUrl;
        for (var i = 0; i < numPages; i++)
        {
            if (i != 0)
            {
                soup = await Soupify($"{baseUrl}{i + 1}/");
            }
    
            var imageList = soup.SelectSingleNode("//div[@class='entry-content gridlane-clearfix']")
                                .SelectNodes("./figure[@class='wp-block-image size-large']")
                                .Select(img => img.SelectSingleNode(".//img").GetSrc());
            images.AddRange(imageList.Select(img => SxChineseGirlzRegex().Replace(img, ""))
                                        .Select(imageUrl => (StringImageLinkWrapper)imageUrl));
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    [GeneratedRegex(@"-\d+x\d+")]
    private static partial Regex SxChineseGirlzRegex();
}
