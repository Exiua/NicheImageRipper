using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class XCancelParser : HtmlParser
{
    public XCancelParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                   FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var baseUrl = CurrentUrl.Split("/").Take(4).Join('/') + "/media";
        if (!CurrentUrl.Contains("/media"))
        {
            CurrentUrl = baseUrl;
        }
        
        var soup = await SolveParse();
        await Task.Delay(500); // Delay to avoid overwhelming the server
        var dirName = soup.SelectNode("//a[@class='profile-card-username']").InnerText[1..]; // Skip the '@' at the start
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var timeline = soup.SelectNode("//div[@class='timeline']")
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
            await Task.Delay(500); // Delay to avoid overwhelming the server
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}