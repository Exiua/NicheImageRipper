using IwaraApiClient;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using Serilog;
using NotSupportedException = NicheImageRipper.Core.Exceptions.NotSupportedException;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class IwaraParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "iwara";

    public IwaraParser(WebDriver driver, ApiClientManager apiClientManager, Dictionary<string, string> requestHeaders,
                       FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<IwaraParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var (username, password) = Config.Logins.Iwara;
        var client = new IwaraClient(username, password);
        string dirName;
        var images = new List<StringImageLinkWrapper>();
        if (CurrentUrl.Contains("/video/"))
        {
            throw new NotImplementedException();
        }
        else if (CurrentUrl.Contains("/profile/"))
        {
            var userId = CurrentUrl.Split('/')[4].Split("?")[0];
            var userResponse = await client.GetUser(userId, cancellationToken);
            if (userResponse is null)
            {
                throw new RipperException($"User not found: {userId}");
            }
            
            dirName = $"{userResponse.User.Username} ({userId})";
            var user = userResponse.User;
            var page = 0;
            while (true)
            {
                var videos = await client.GetUserVideos(user.Id, page, cancellationToken: cancellationToken);
                if (videos is null)
                {
                    Logger.Warning("Videos not found for user {UserId}", userId);
                    break;
                }

                foreach (var video in videos.Results)
                {
                    var videoLink = $"https://www.iwara.tv/video/{video.Id}";
                    var imageLink = new ImageLink(videoLink, FilenameScheme, 0)
                    {
                        Filename = video.Title + ".mp4",
                        LinkInfo = LinkInfo.M3U8YtDlp,
                    };
                    
                    images.Add(imageLink);
                }

                // When retrieved fewer items than limit
                if (videos.Count < videos.Limit)
                {
                    break;
                }
                
                page++;
            }
            
            page = 0;
            while (true)
            {
                var imagesResponse = await client.GetUserImages(user.Id, page, cancellationToken: cancellationToken);
                if (imagesResponse is null)
                {
                    Logger.Warning("Images not found for user {UserId}", userId);
                    break;
                }

                foreach (var image in imagesResponse.Results)
                {
                    foreach (var file in image.Files)
                    {
                        var imageLink =
                            $"https://i.iwara.tv/image/original/{file.Id}/{file.Name}";
                        images.Add(imageLink);
                    }
                }

                // When fewer items than limit were returned
                if (imagesResponse.Count < imagesResponse.Limit)
                {
                    break;
                }
                
                page++;
            }
        }
        else
        {
            throw new NotSupportedException("IwaraParser", $"Unsupported URL format: {CurrentUrl}");
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}