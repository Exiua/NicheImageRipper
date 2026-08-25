

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Love4Porn;
public class Love4PornParser : HtmlParser
{
    public Love4PornParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the HTML for love4porn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var id = CurrentUrl.Split("/")[4];
        if (CurrentUrl.EndsWith('/'))
        {
            CurrentUrl += "?asgtbndr=1&play=true";
        }
        else
        {
            CurrentUrl += "&asgtbndr=1&play=true";
        }

        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText + $" ({id})";
        var images = new List<StringFileLinkWrapper>();
        var url = soup.SelectSingleNodeOrThrow("//video").GetSrc();
        images.Add(url);
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}