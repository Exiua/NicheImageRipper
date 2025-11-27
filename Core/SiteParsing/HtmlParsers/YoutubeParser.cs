using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class YoutubeParser : HtmlParser
{
    public YoutubeParser(WebDriver driver, ApiClientManager apiClientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        // TODO: May be able to rectify this in PostProcess by renaming files after download
        if (FilenameScheme != FilenameScheme.Original)
        {
            Log.Warning("YoutubeParser only supports Original filename scheme. Files will be saved with original filenames.");
        }
        
        var soup = await Soupify();
        var displayName = soup.SelectSingleNodeOrThrow("//h1[@class='dynamicTextViewModelH1']/span").InnerText;
        var username = soup
                      .SelectSingleNodeOrThrow(
                           "//span[@class='yt-core-attributed-string yt-content-metadata-view-model__metadata-text yt-core-attributed-string--white-space-pre-wrap yt-core-attributed-string--link-inherit-color']")
                      .InnerText;
        var dirName = $"{displayName} ({username})";
        var imageLink = new ImageLink(CurrentUrl, FilenameScheme, 0)
        {
            LinkInfo = LinkInfo.YoutubeChannel,
        };

        var images = new List<StringImageLinkWrapper> { imageLink };

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}