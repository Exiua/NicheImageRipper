using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Saint2Parser : HtmlParser
{
    public Saint2Parser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        return await Saint2Parse("");
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> Saint2Parse(string url)
    {
        if (url != "")
        {
            CurrentUrl = url;
        }
        
        var soup = await Soupify();
        string dirName;
        var images = new List<StringImageLinkWrapper>();
        if (CurrentUrl.Contains("/embed/"))
        {
            dirName = "Saint2 Video";
            var downloadLink = soup.SelectSingleNode("//a[@class='plyr__controls__item plyr__control']").GetHref();
            soup = await Soupify(downloadLink);
            var link = soup.SelectSingleNode("//a").GetHref();
            images.Add(link);
        }
        else
        {
            throw new RipperException($"Unhandled url: {CurrentUrl}");
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
