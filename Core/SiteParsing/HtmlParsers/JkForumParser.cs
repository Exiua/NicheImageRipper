using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class JkForumParser : HtmlParser
{
    public JkForumParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for jkforum.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(delay: 1000);
        var dirName = soup.SelectSingleNode("//div[@class='title-cont']")
                            .SelectSingleNode(".//h1")
                            .InnerText;
        var images = soup.SelectSingleNode("//td[@class='t_f']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc().Remove(".thumb.jpg"))
                            .ToStringImageLinkWrapperList();
        // TODO: Find a way to download videos as well
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
