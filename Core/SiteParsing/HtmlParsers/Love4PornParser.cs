using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Love4PornParser : HtmlParser
{
    public Love4PornParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for love4porn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
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

        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText + $" ({id})";
        var images = new List<StringImageLinkWrapper>();
        var url = soup.SelectSingleNodeOrThrow("//video").GetSrc();
        images.Add(url);

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}