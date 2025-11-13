using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class TsuminoParser : HtmlParser
{
    public TsuminoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for tsumino.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='book-title']")
                            .InnerText;
        var numPages = int.Parse(soup.SelectSingleNodeOrThrow("//div[@id='Pages']")
                                    .InnerText
                                    .Trim());
        var pagerUrl = CurrentUrl.Replace("/entry/", "/Read/Index/") + "?page=";
        var images = new List<StringImageLinkWrapper>();
        for (var i = 1; i <= numPages; i++)
        {
            soup = await Soupify($"{pagerUrl}{i}", delay: 3000);
            var src = soup.SelectSingleNodeOrThrow("//img[@class='img-responsive reader-img']")
                            .GetSrc()
                            .Replace("&amp;", "&");
            images.Add(src);
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
