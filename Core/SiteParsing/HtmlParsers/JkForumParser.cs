using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class JkForumParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "jkforum";

    public JkForumParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<JkForumParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for jkforum.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(delay: 1000);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='title-cont']")
                            .SelectSingleNodeOrThrow(".//h1")
                            .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//td[@class='t_f']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => img.GetSrc().Remove(".thumb.jpg"))
                            .ToStringImageLinkWrapperList();
        // TODO: Find a way to download videos as well
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
