using HtmlAgilityPack;


using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.BabeImpact;
public class BabeImpactParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "babeimpact";
    public static string[] SupportedUrls => ["https://www.babeimpact.com/"];

    public BabeImpactParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabeImpactParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for babeimpact.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var title = soup.SelectSingleNodeOrThrow("//h1[@class='blockheader pink center lowercase']").InnerText;
        var sponsor = soup.SelectSingleNodeOrThrow("//div[@class='c']").SelectNodesOrThrow(".//a")[1].InnerText.Trim();
        sponsor = $"({sponsor})";
        var dirName = $"{sponsor} {title}";
        var tags = soup.SelectNodesOrThrow("//div[@class='list gallery']");
        var tagList = new List<HtmlNode>();
        foreach (var tag in tags)
        {
            tagList.AddRange(tag.SelectNodesOrThrow(".//div[@class='item']"));
        }

        var images = new List<StringFileLinkWrapper>();
        var imageList = tagList.Select(tag => tag.SelectSingleNodeOrThrow(".//a")).Select(anchor => $"https://babeimpact.com{anchor.GetHref()}").ToList();
        foreach (var image in imageList)
        {
            soup = await Soupify(image, cancellationToken: cancellationToken);
            var img = soup.SelectSingleNodeOrThrow("//div[@class='image-wrapper']").SelectSingleNodeOrThrow(".//img").GetSrc();
            images.Add((StringFileLinkWrapper)(Protocol + img));
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}