using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class XCancelParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "xcancel";

    public XCancelParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                   FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders,
        IHtmlParser.GetFilenameScheme<XCancelParser>(filenameScheme))
    {
    }

    private static int GenerateDelay()
    {
        return Random.Shared.Next(1000, 3500); // Random delay between 1 and 3.5 seconds
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var baseUrl = CurrentUrl.Split("/").Take(4).Join('/') + "/media";
        if (!CurrentUrl.Contains("/media"))
        {
            CurrentUrl = baseUrl;
        }
        
        // TODO: Add anti-bot page detection handling
        var soup = await SolveParse();
        await Task.Delay(GenerateDelay()); // Delay to avoid overwhelming the server
        var dirName = soup.SelectSingleNodeOrThrow("//a[@class='profile-card-username']").InnerText[1..]; // Skip the '@' at the start
        var images = new List<StringImageLinkWrapper>();
        var page = 1;
        while (true)
        {
            Logger.Information("Parsing page {page}", page);
            page++;
            var timeline = soup.SelectSingleNodeOrThrow("//div[@class='timeline']")
                               .SelectNodesSafe("./div[@class='timeline-item ']"); // Class name has a trailing space
            foreach (var div in timeline)
            {
                var imgs = div.SelectNodesSafe(".//a[@class='still-image']")
                             .Select(a => a.GetHref())
                              .ToStringImageLinks();
                images.AddRange(imgs);
                var video = div.SelectSingleNode(".//video/source")?
                                .GetSrc();
                if (video is not null)
                {
                    images.Add(video);
                }
            }

            var nextButton = soup.SelectSingleNode("//div[@class='show-more']/a");
            if (nextButton is null)
            {
                break;
            }
            
            var nextUrl = nextButton.GetHref();
            CurrentUrl = $"{baseUrl}{nextUrl}";
            soup = await SolveParse();
            var multiplier = page % 10 == 0 ? 10 : 1; // Increase delay every 10 pages to avoid overwhelming the server
            await Task.Delay(GenerateDelay() * multiplier); // Delay to avoid overwhelming the server
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}