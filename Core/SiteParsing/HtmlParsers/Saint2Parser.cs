using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class Saint2Parser : ParameterizedHtmlParser
{
    public Saint2Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(string url, CancellationToken cancellationToken = default)
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
            var downloadLink = soup.SelectSingleNodeOrThrow("//a[@class='plyr__controls__item plyr__control']").GetHref();
            soup = await Soupify(downloadLink);
            var link = soup.SelectSingleNodeOrThrow("//a").GetHref();
            images.Add(link);
        }
        else
        {
            throw new RipperException($"Unhandled url: {CurrentUrl}");
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
