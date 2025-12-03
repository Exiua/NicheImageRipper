using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class MangaParkParser : HtmlParser
{
    public MangaParkParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                           FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for mangapark.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        List<Dictionary<string, string>> cookies = [
            new()
            {
                ["name"] = "nsfw",
                ["value"] = "2"
            }
        ];
        var soup = await SolveParseAddCookies(cookies: cookies);
        Driver.SetCookie("nsfw", "2");;
        var dirName = soup.SelectSingleNodeOrThrow("//h3[@class='text-lg md:text-2xl font-bold']/a").InnerText;
        var chapterList = soup.SelectSingleNodeOrThrow("//div[@data-name='chapter-list']")
                              .SelectNodesOrThrow("./div")[1]
                              .SelectSingleNodeOrThrow("./div/div")
                              .SelectNodesOrThrow("./div")
                              .Select(div => div.SelectSingleNodeOrThrow(".//a").GetHref())
                              .Reverse();
        var images = new List<StringImageLinkWrapper>();
        foreach (var chapter in chapterList)
        {
            var chapterUrl = $"https://mangapark.net{chapter}";
            Log.Debug("Parsing chapter {ChapterUrl}", chapterUrl);
            soup = await Soupify(chapterUrl, xpath: "//div[@data-name='image-item']");
            var pages = soup.SelectNodesOrThrow("//div[@data-name='image-item']")
                            .Select(div => div.SelectSingleNodeOrThrow(".//img").GetSrc())
                            .ToStringImageLinks();
            images.AddRange(pages);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}