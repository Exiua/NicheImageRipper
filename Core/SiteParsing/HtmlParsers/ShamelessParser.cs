using System.Text.Json;
using System.Text.Json.Nodes;
using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ShamelessParser : HtmlParser
{
    public ShamelessParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                           FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for shameless.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h4[@class='title']").InnerText;
        var script = soup.SelectSingleNodeOrThrow("//div[@class='player-holder']//script[not(@src)]")
                         .InnerText
                         .Split("var flashvars = ")[1]
                         .Split("};")[0] + "}";
        var rawJson = ExtractJsonObject(script);
        var url = rawJson.Split("video_url:")[1].Split(",")[0].Trim().Trim('\'', '"');
        var images = new List<StringImageLinkWrapper> { url };

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}