using System.Text.Json;
using System.Text.Json.Serialization;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.Sdk.Utility;


using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Flickr;

public class FlickrParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "flickr";
    public static string[] SupportedUrls => ["https://www.flickr.com/"];

    public FlickrParser(WebDriver driver,  Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, 
        requestHeaders, IHtmlParser.GetFilenameScheme<FlickrParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for flickr.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs { ScrollBy = true },
            cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
        var images = new List<StringFileLinkWrapper>();
        var imagePosts = new List<string>();
        var pageCount = 1;
        while (true)
        {
            Logger.Information("Parsing page {pageCount}", pageCount);
            pageCount += 1;
            var posts = soup
                       .SelectSingleNodeOrThrow(
                            "//div[contains(@class, 'view') and contains(@class, 'photo-list-view') and contains(@class, 'photostream')]")
                       .SelectNodesOrThrow(
                            ".//div[contains(@class, 'view') and contains(@class, 'photo-list-photo-view') and contains(@class, 'photostream')]")
                       .Select(post => post.SelectSingleNodeOrThrow(".//a[@class='overlay']").GetHref())
                       .Select(dummy => $"https://www.flickr.com{dummy}").ToList();
            imagePosts.AddRange(posts);
            var nextButton = soup.SelectSingleNode("//a[@rel='next']");
            if (nextButton is not null)
            {
                var nextUrl = nextButton.GetHref();
                soup = await Soupify($"https://www.flickr.com{nextUrl}",
                    lazyLoadArgs: new LazyLoadArgs { ScrollBy = true }, cancellationToken: cancellationToken);
            }
            else
            {
                break;
            }
        }

        foreach (var (i, post)in imagePosts.Enumerate())
        {
            Logger.Information("Parsing post {i}: {post}", i + 1, post);
            var delay = 100;
            soup = await Soupify(post, delay: delay, cancellationToken: cancellationToken);
            var script = soup.SelectSingleNodeOrThrow("//script[@class='modelExport']").InnerText;
            string paramValues;
            while (true)
            {
                try
                {
                    paramValues = "{\"photoModel\"" + script.Split("{\"photoModel\"")[1];
                    break;
                }
                catch (IndexOutOfRangeException)
                {
                    delay *= 2;
                    soup = await Soupify(post, delay: delay, cancellationToken: cancellationToken);
                    script = soup.SelectSingleNodeOrThrow("//script[@class='modelExport']").InnerText;
                }
            }

            paramValues = JsonUtility.ExtractJsonObject(paramValues);
            var paramsJson = JsonSerializer.Deserialize<ModelExport>(paramValues)!;
            var imgUrl = paramsJson.PhotoModel.DescendingSizes[0].Url;
            images.Add(Protocol + imgUrl);
        }

        return await RipInfo.FromUrlListAsync(images, dirName, FilenameScheme);
    }

    private class ModelExport
    {
        [JsonPropertyName("photoModel")]
        public PhotoModel PhotoModel { get; set; } = null!;
    }

    private class PhotoModel
    {
        [JsonPropertyName("descendingSizes")]
        public List<PhotoSize> DescendingSizes { get; set; } = null!;
    }

    private class PhotoSize
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = null!;
    }
}