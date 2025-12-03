using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class MeijuntuParser : HtmlParser
{
    public MeijuntuParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for meijuntu.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='title']").InnerText;
        var pageCount = soup.SelectSingleNodeOrThrow("//div[@id='pages']")
                            .SelectNodesOrThrow("./a")[^2]
                            .InnerText
                            .ToInt();
        var baseUrl = CurrentUrl.Remove(".html");
        var images = new List<StringImageLinkWrapper>();
        for(var i = 0; i < pageCount; i++)
        {
            var img = soup.SelectSingleNodeOrThrow("//div[@class='pictures']/img").GetSrc();
            images.Add(img);
            if (i != pageCount - 1)
            {
                soup = await Soupify($"{baseUrl}-{i+2}.html");
            }
            
            await Sleep(250);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}