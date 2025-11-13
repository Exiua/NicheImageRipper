using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class GoogleParser : HtmlParser
{
    public GoogleParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Query the Google Drive API to get file information to download
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        return await GoogleParse("");
    }

    /// <summary>
    ///     Query the Google Drive API to get file information to download
    /// </summary>
    /// <param name="gdriveUrl">The url to parse (default: CurrentUrl)</param>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> GoogleParse(string gdriveUrl)
    {
        if (string.IsNullOrEmpty(gdriveUrl))
        {
            gdriveUrl = CurrentUrl;
        }
    
        // Actual querying happens within the RipInfo object
        return Task.FromResult(RipInfo.FromUrlList([gdriveUrl], "", FilenameScheme));
    }
}
