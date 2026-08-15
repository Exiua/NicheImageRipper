using HtmlAgilityPack;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Eporner;
public class EpornerParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "eporner";
    public static string[] SupportedUrls => ["https://www.eporner.com/"];

    public EpornerParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EpornerParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for eporner.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        const string domainUrl = "https://www.eporner.com";
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 250
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
        string dirName;
        var images = new List<StringFileLinkWrapper>();
        if (CurrentUrl.Contains("/profile/"))
        {
            var baseUrl = CurrentUrl.Split("/")[..5].Join("/");
            dirName = soup.SelectSingleNodeOrThrow("//div[@id='pprofiletopinfo']//h1").InnerText;
            var headers = soup.SelectSingleNodeOrThrow("//div[@id='pnavtop']").SelectNodesOrThrow("./a");
            var profileSections = EpornerProfileSections.None;
            foreach (var header in headers)
            {
                switch (header.InnerText)
                {
                    case "Videos":
                        profileSections |= EpornerProfileSections.Videos;
                        break;
                    case "Pics / GIFs":
                        profileSections |= EpornerProfileSections.Images;
                        break;
                    case "Playlists":
                        profileSections |= EpornerProfileSections.Playlists;
                        break;
                }
            }

            var posts = new List<string>();
#region Extract Videos
            if (profileSections.HasFlag(EpornerProfileSections.Videos))
            {
                await ExtractFromListView(baseUrl, "uploaded-videos", "streameventsday showAll", posts, cancellationToken: cancellationToken);
                // if (profileSections.HasFlag(EpornerProfileSections.Playlists))
                // {
                //     await ExtractFromListView(baseUrl, "playlists", "streameventsday showAll", posts);
                // }
                foreach (var post in posts)
                {
                    Logger.Information("Parsing video: {post}", post);
                    soup = await Soupify(post, lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
                    var downloadLink = ExtractVideoDownloadLink(soup);
                    images.Add(downloadLink);
                }
            }

#endregion
#region Extract Images
            posts.Clear();
            if (profileSections.HasFlag(EpornerProfileSections.Images))
            {
                await ExtractFromListView(baseUrl, "uploaded-pics", "streameventsday photosgrid showAll", posts, cancellationToken: cancellationToken);
                foreach (var post in posts)
                {
                    Logger.Information("Parsing gallery: {post}", post);
                    soup = await Soupify(post, lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
                    var postImages = soup.SelectSingleNodeOrThrow("//div[@class='photosgrid gallerygrid']").SelectNodesOrThrow("./div").Select(div => div.SelectSingleNodeOrThrow(".//img").GetSrc()).Select(ExtractFullImageLink).ToStringImageLinks();
                    images.AddRange(postImages);
                }
            }
#endregion
        }
        else if (CurrentUrl.Contains("/video-") || CurrentUrl.Contains("/hd-porn/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//div[@id='video-info']/h1").InnerText;
            var downloadLink = ExtractVideoDownloadLink(soup);
            images.Add(downloadLink);
        }
        else
        {
            var e = new RipperException("Unknown Eporner URL type");
            Logger.Error(e, "Unknown Eporner URL type: {CurrentUrl}", CurrentUrl);
            throw e;
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
        // ReSharper disable once VariableHidesOuterVariable
        string ExtractVideoDownloadLink(HtmlNode soup)
        {
            var downloads = soup.SelectSingleNodeOrThrow("//div[@id='hd-porn-dload']").SelectNodesOrThrow(".//span");
            string downloadLink;
            if (downloads.Count > 1)
            {
                var secondLast = downloads[^2];
                if (secondLast.GetAttributeValue("class") == "download-av1")
                {
                    downloadLink = secondLast.SelectSingleNodeOrThrow("./a").GetHref();
                }
                else
                {
                    var last = downloads[^1];
                    downloadLink = last.SelectSingleNodeOrThrow("./a").GetHref();
                }
            }
            else
            {
                var last = downloads[^1];
                downloadLink = last.SelectSingleNodeOrThrow("./a").GetHref();
            }

            return $"https://www.eporner.com{downloadLink}";
        }

        string ExtractFullImageLink(string imageLink)
        {
            var parts = imageLink.Split("/");
            var baseLink = parts[..^1].Join("/");
            var filename = parts[^1];
            var extension = filename.Split(".")[^1];
            filename = filename.Split("_")[0];
            var link = $"{baseLink}/{filename}.{extension}";
            return link;
        }

        async Task ExtractFromListView(string baseUrl, string section, string divClass, List<string> posts, CancellationToken cancellationToken = default)
        {
            // ReSharper disable once VariableHidesOuterVariable
            var soup = await Soupify($"{baseUrl}/{section}/", lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
            while (true)
            {
                var links = soup.SelectSingleNodeOrThrow($"//div[@class='{divClass}']").SelectNodesOrThrow("./div[contains(@class, 'mb')]").Select(div => domainUrl + div.SelectSingleNodeOrThrow(".//a").GetHref());
                posts.AddRange(links);
                var nextPage = soup.SelectSingleNode("//a[@class='nmnext']");
                if (nextPage is null)
                {
                    break;
                }

                var nextPageUrl = nextPage.GetHref();
                soup = await Soupify($"{domainUrl}{nextPageUrl}", cancellationToken: cancellationToken);
            }
        }
    }
}