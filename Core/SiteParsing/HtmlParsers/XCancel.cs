using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class XCancel : HtmlParser
{
    public XCancel(WebDriver driver, Dictionary<string, string> requestHeaders,
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
        if (!CurrentUrl.Contains("/media"))
        {
            var mediaUrl = CurrentUrl.Split("/").Take(4).Join('/') + "/media";
            CurrentUrl = mediaUrl;
        }
        
        var soup = await Soupify(xpath: "//div[@class='timeline']");
        var dirName = soup.SelectNode("//a[@class='profile-card-username']").InnerText[1..]; // Skip the '@' at the start
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var timeline = soup.SelectNode("//div[@class='timeline']")
                               .SelectNodesSafe("./div[@class='timeline-item']");
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
            soup = await Soupify(nextUrl, delay: 250, xpath: "//div[@class='timeline']");
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}