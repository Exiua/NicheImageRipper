using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using OpenQA.Selenium;
using HtmlAgilityPack;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.SiteModules.SiteParsing.VideoCapturers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class TwitterParser : HtmlParser, IHtmlParser, IMultiSiteHtmlParser
{
    public static string ParserName => "twitter";
    public static string[] SupportedUrls => ["https://x.com/"];
    public static string[] AdditionalParserNames { get; } = ["x"];

    public TwitterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<TwitterParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for twitter.com/x.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
#region Method-Global Variables
        const int trueBaseWaitTime = 2500;
        const int baseWaitTime = trueBaseWaitTime;
        const int maxWaitTime = 60000;
        var waitTime = baseWaitTime;
        const int maxRetries = 4;
        var streak = 0;
#endregion
        var cookieValue = Config.Cookies.Twitter;
        var cookieJar = Driver.GetCookieJar();
        cookieJar.AddCookie("auth_token", cookieValue);
        var dirName = CurrentUrl.Split("/")[3];
        if (!CurrentUrl.Contains("/media"))
        {
            CurrentUrl = $"https://twitter.com/{dirName}/media";
        }

        await Task.Delay(waitTime, cancellationToken);
        var postLinks = new OrderedHashSet<string>();
        var newPosts = true;
        while (newPosts)
        {
            newPosts = false;
            var soup = await Soupify(cancellationToken: cancellationToken);
            var rows = soup.SelectSingleNodeOrThrow("//section").SelectSingleNodeOrThrow(".//div").SelectNodesOrThrow("./div");
            foreach (var row in rows)
            {
                var posts = row.SelectNodesOrThrow(".//li");
                foreach (var post in posts)
                {
                    var postLinkNode = post.SelectSingleNode(".//a");
                    if (postLinkNode is null)
                    {
                        continue;
                    }

                    var postLink = postLinkNode.GetHref();
                    if (postLinks.Add(postLink))
                    {
                        newPosts = true;
                    }
                }
            }

            if (newPosts)
            {
                ScrollPage();
                await Task.Delay(waitTime, cancellationToken);
            }
        }

        var(capturer, b) = await ConfigureNetworkCapture<TwitterVideoCapturer>(cancellationToken);
        await using var bidi = b;
        // var capturer = new TwitterVideoCapturer();
        // var bidi = await Driver.AsBiDiAsync();
        // await bidi.Network.OnResponseCompletedAsync(capturer.CaptureHook);
        var images = new List<StringFileLinkWrapper>();
        var failedUrls = new List<(int, string)>();
        foreach (var(i, link)in postLinks.Enumerate())
        {
            var postLink = $"https://twitter.com{link}";
            Logger.Information("Post {index}/{totalLinks}: {postLink}", i + 1, postLinks.Count, postLink);
            var(links, retryUrls) = await TwitterParserHelper(postLink, false);
            var videoLinks = capturer.GetNewVideoLinks();
            images.AddRange(videoLinks.ToStringImageLinks());
            var totalFound = images.Count;
            images.AddRange(links.ToStringImageLinks());
            foreach (var(j, url)in retryUrls)
            {
                failedUrls.Add((totalFound + j, url));
            }
        }

        if (failedUrls.Count > 0)
        {
            cookieJar.DeleteAllCookies();
            cookieJar.AddCookie("auth_token", cookieValue);
            capturer.Flush();
            await Task.Delay(600_000); // Wait 10 minutes before retrying failed links
            var failed = failedUrls.Select(url => url).ToList();
            failedUrls = [];
            var offset = 0;
            foreach (var(i, (index, link))in failed.Enumerate())
            {
                Logger.Information("Retrying failed post {index}/{totalLinks}: {postLink}", i + 1, failed.Count, link);
                var(links, retryUrls) = await TwitterParserHelper(link, true);
                var linkTemp = links.Select(x => (StringFileLinkWrapper)x);
                failedUrls.AddRange(retryUrls);
                images.InsertRange(index + offset, linkTemp);
                offset += links.Count;
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
        async Task<(List<string>, List<(int, string)>)> TwitterParserHelper(string postLink, bool logFailure)
        {
            var postImages = new List<string>();
            // ReSharper disable once VariableHidesOuterVariable
            var failedUrls = new List<(int, string)>();
            var found = 0;
            for (var i = 0; i < maxRetries; i++)
            {
                var mediaFound = false;
                try
                {
                    var soup = await Soupify(postLink, delay: baseWaitTime, xpath: "//article", cancellationToken: cancellationToken);
                    var articles = soup.SelectNodesOrThrow("//article");
                    foreach (var article in articles)
                    {
                        var content = article.SelectSingleNodeOrThrow("./div").SelectSingleNodeOrThrow("./div");
                        var temp = content.SelectNodesOrThrow("./div");
                        content = temp.Count > 2 ? temp[2] : temp[1];
                        temp = content.SelectNodesOrThrow("./div");
                        if (temp.Count <= 1)
                        {
                            continue; // Most likely comment section
                        }

                        content = content.SelectNodesOrThrow("./div")[1];
                        var imgs = content.SelectNodesSafe(".//img");
                        foreach (var img in imgs)
                        {
                            var link = img.GetSrc().Replace("&amp;", "&");
                            if (link.Contains("/emoji/"))
                            {
                                continue;
                            }

                            if (link.Contains("&name="))
                            {
                                link = link.Split("&name=")[0];
                                link = $"{link}&name=4096x4096"; // Get the highest resolution image
                            }

                            postImages.Add(link);
                            found += 1;
                            mediaFound = true;
                        }

                        var gifs = content.SelectNodesSafe(".//video");
                        foreach (var gif in gifs)
                        {
                            postImages.Add(gif.GetSrc());
                            found += 1;
                            mediaFound = true;
                        }
                    }

                    break;
                }
                catch
                {
                    streak = 0;
                    var waitBoost = 0;
                    if (i == maxRetries - 1)
                    {
                        Logger.Warning("Failed to get media: {PostLink}", postLink);
                        if (logFailure)
                        {
                            DownloadLogging.LogFailedUrl(postLink);
                        }

                        failedUrls.Add((found, postLink));
                        continue;
                    }

                    if (i == maxRetries - 2)
                    {
                        //baseWaitTime = waitTime
                        waitBoost = 300_000; // Wait 5 minutes before retrying
                    // Arbitrary, but should be enough time to get around rate limiting
                    }

                    waitTime = Math.Min(baseWaitTime * (int)Math.Pow(2, i), maxWaitTime);
                    var jitter = (int)(Random.Shared.NextDouble() * waitTime);
                    waitTime += jitter + waitBoost;
                    Logger.Warning("Attempt {Attempt} failed. Retrying in {WaitTime:F2} seconds...", i + 1, waitTime / 1000.0);
                    await Task.Delay(waitTime, cancellationToken);
                }

                if (!mediaFound)
                {
                    continue;
                }

                if (i == 0)
                {
                    streak += 1;
                    if (streak == 3)
                    {
                        //baseWaitTime *= 0.9; // Reduce wait time if successful on first try
                        //baseWaitTime = Math.Max(baseWaitTime, trueBaseWaitTime);
                    }
                }

                break;
            }

            return (postImages, failedUrls);
        }
    }
}