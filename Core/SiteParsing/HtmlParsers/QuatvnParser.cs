using System.Text.Json;
using System.Text.Json.Nodes;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class QuatvnParser : HtmlParser
{
    public QuatvnParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for quatvn.love and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='g1-mega g1-mega-1st entry-title']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var container =
            soup.SelectSingleNode(
                "//div[@id='content']//div[@class='g1-content-narrow g1-typography-xl entry-content']");
        var gallery = container.SelectSingleNode(".//figure[@class='mace-gallery-teaser']");
        if (gallery is not null)
        {
            var imageData = gallery.GetDataAttribute("data-g1-gallery");
            var imageList = JsonSerializer.Deserialize<JsonNode>(imageData.Value)!.AsArray();
            var imgs = imageList.Select(entry => entry!.AsObject()["full"]!.Deserialize<string>());
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
