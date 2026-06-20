using Common.ExtensionMethods;
using HtmlAgilityPack;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class BabeImpactParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "babeimpact";

    public BabeImpactParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabeImpactParser>(filenameScheme))
    {
    }
    
    /// <summary>
    ///     Parses the html for babeimpact.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var title = soup.SelectSingleNodeOrThrow("//h1[@class='blockheader pink center lowercase']").InnerText;
        var sponsor = soup.SelectSingleNodeOrThrow("//div[@class='c']")
                          .SelectNodesOrThrow(".//a")[1]
                          .InnerText
                          .Trim();
        sponsor = $"({sponsor})";
        var dirName = $"{sponsor} {title}";
        var tags = soup.SelectNodesOrThrow("//div[@class='list gallery']");
        var tagList = new List<HtmlNode>();
        foreach (var tag in tags)
        {
            tagList.AddRange(tag.SelectNodesOrThrow(".//div[@class='item']"));
        }

        var images = new List<StringImageLinkWrapper>();
        var imageList = tagList.Select(tag => tag.SelectSingleNodeOrThrow(".//a")).Select(anchor => $"https://babeimpact.com{anchor.GetHref()}").ToList();
        foreach (var image in imageList)
        {
            soup = await Soupify(image);
            var img = soup.SelectSingleNodeOrThrow("//div[@class='image-wrapper']")
                          .SelectSingleNodeOrThrow(".//img")
                          .GetSrc();
            images.Add((StringImageLinkWrapper)(Protocol + img));
        }
        
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}