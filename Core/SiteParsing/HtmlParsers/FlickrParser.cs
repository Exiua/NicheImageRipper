using System.Text.Json;
using System.Text.Json.Nodes;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class FlickrParser : HtmlParser
{
    public FlickrParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for flickr.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNode("//h1").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var imagePosts = new List<string>();
        var pageCount = 1;
        while (true)
        {
            Log.Information("Parsing page {pageCount}", pageCount);
            pageCount += 1;
            var posts = soup
                        .SelectSingleNode("//div[contains(@class, 'view') and contains(@class, 'photo-list-view') and contains(@class, 'photostream')]")
                        .SelectNodes(".//div[contains(@class, 'view') and contains(@class, 'photo-list-photo-view') and contains(@class, 'photostream')]")
                        .Select(post => post.SelectSingleNode(".//a[@class='overlay']").GetHref())
                        .Select(dummy => $"https://www.flickr.com{dummy}")
                        .ToList();
            imagePosts.AddRange(posts);
            var nextButton = soup.SelectSingleNode("//a[@rel='next']");
            if (nextButton is not null)
            {
                var nextUrl = nextButton.GetHref();
                soup = await Soupify($"https://www.flickr.com{nextUrl}", lazyLoadArgs: new LazyLoadArgs
                {
                    ScrollBy = true
                });
            }
            else
            {
                break;
            }
        }
        
        foreach (var (i, post) in imagePosts.Enumerate())
        {
            Log.Information("Parsing post {i}: {post}", i + 1, post);
            var delay = 100;
            soup = await Soupify(post, delay: delay);
            var script = soup.SelectSingleNode("//script[@class='modelExport']").InnerText;
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
                    soup = await Soupify(post, delay: delay);
                    script = soup.SelectSingleNode("//script[@class='modelExport']").InnerText;
                }
            }
    
            paramValues = ExtractJsonObject(paramValues);
            var paramsJson = JsonSerializer.Deserialize<JsonNode>(paramValues);
            var imgUrl = paramsJson?
                        .AsObject()["photoModel"]!
                        .AsObject()["descendingSizes"]!
                        .AsArray()[0]!
                        .AsObject()["url"]
                        .Deserialize<string>()!;
            images.Add(Protocol + imgUrl);
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
