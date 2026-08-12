using HtmlAgilityPack;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class BunkrParser : ParameterizedHtmlParser, IHtmlParser
{
    public static string ParserName => "bunkr";
    public static string[] SupportedUrls => ["https://bunkr.si/"];

    private const int ParseDelay = 500;
    public BunkrParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BunkrParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for bunkr.si and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var notFound = soup.SelectSingleNode("//h1[@class='text-3xl font-bold']");
        if (notFound is not null)
        {
            Logger.Warning("Page not found: {CurrentUrl}", CurrentUrl);
            return RipInfo.Empty;
        }

        string dirName;
        if (IsSubParserCall)
        {
            dirName = "internal-use";
        }
        else
        {
            var dirNameNode = soup.SelectSingleNode("//h1[@class='text-[24px] font-bold text-dark dark:text-white']");
            if (dirNameNode is null)
            {
                dirNameNode = soup.SelectSingleNodeOrThrow("//h1[@class='truncate']");
            }

            dirName = dirNameNode.InnerText;
        }

        List<StringFileLinkWrapper> images = [];
        StringFileLinkWrapper? link = null;
        if (CurrentUrl.Contains("/a/"))
        {
            var grid = soup.SelectSingleNode("//div[@class='grid-images']");
            if (grid is not null)
            {
                goto GridInitializationEnd;
            }

            grid = soup.SelectSingleNodeOrThrow("//div[@class='grid gap-4 grid-cols-repeat [--size:11rem] lg:[--size:14rem] grid-images']");
            GridInitializationEnd:
                var imagePosts = grid.SelectNodesOrThrow(".//a");
            foreach (var(i, post)in imagePosts.Enumerate())
            {
                var href = post.GetHref();
                if (href[0] == '/')
                {
                    href = $"https://bunkr.cr{href}";
                }

                Logger.Debug("Post {index} of {total}: {href}", i + 1, imagePosts.Count, href);
                const int maxRetries = 4;
                for (var j = 0; j < maxRetries; j++)
                {
                    try
                    {
                        soup = await Soupify(href, delay: ParseDelay, cancellationToken: cancellationToken);
                        if (Driver.Title == "502 Bad Gateway")
                        {
                            Driver.Refresh();
                            soup = await Soupify(href, delay: ParseDelay * 2, cancellationToken: cancellationToken);
                        }

                        break;
                    }
                    catch (WebDriverException e)when (e.Message.StartsWith("The HTTP request to the remote WebDriver server for URL"))
                    {
                        if (j == maxRetries - 1)
                        {
                            throw new RipperException("Failed to load page", e);
                        }

                        Logger.Debug("Failed to load page, retrying...");
                        await Task.Delay(ParseDelay * 4);
                        WebDriver.RegenerateDriver();
                    }
                }

                link = await ResolveLinkForCurrentUrl(soup);
                if (link is not null)
                {
                    images.Add(link.Value);
                    link = null;
                }
            }
        }
        else
        {
            link = await ResolveLinkForCurrentUrl(soup);
        }

        if (link is not null)
        {
            images.Add(link.Value);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<StringFileLinkWrapper?> ResolveLinkForCurrentUrl(HtmlNode soup)
    {
        if (CurrentUrl.Contains("/i/"))
        {
            return GetImageLink(soup);
        }

        if (CurrentUrl.Contains("/v/") || CurrentUrl.Contains("/f/"))
        {
            return await GetVideoLink(soup);
        }

        if (CurrentUrl.Contains("/d/"))
        {
            return await GetDownloadLink(soup);
        }

        Logger.Warning("Unknown type: {CurrentUrl}", CurrentUrl);
        return null;
    }

    private async Task<FileLink> GetVideoLink(HtmlNode soup, CancellationToken cancellationToken = default)
    {
        var videoDownloadNode = soup.SelectSingleNode("//a[@id='czmDownloadz']");
        string videoDownload;
        if (videoDownloadNode is not null)
        {
            videoDownload = videoDownloadNode.GetHref();
        }
        else
        {
            videoDownload = soup.SelectSingleNodeOrThrow("//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold flex-1 ic-download-01 ic-before before:text-lg']").GetHref();
        }

        soup = await Soupify(videoDownload, xpath: "//main//video", delay: ParseDelay, cancellationToken: cancellationToken);
        var video = soup.SelectSingleNode("//main//video");
        var downloadButton = soup.SelectSingleNodeOrThrow("//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg']");
        var downloadUrl = downloadButton.GetHref();
        var filename = video is not null ? video.GetSrc().Split("/")[^1] : downloadUrl.Split("/")[^1];
        var videoLink = FileLink.WithFilename(downloadUrl, filename, FilenameScheme);
        return videoLink;
    }

    private static string GetImageLink(HtmlNode soup)
    {
        var img = soup.SelectSingleNodeOrThrow("//main//img");
        return img.GetSrc();
    }

    private async Task<string> GetDownloadLink(HtmlNode soup, CancellationToken cancellationToken = default)
    {
        var downloadLinkNode = soup.SelectSingleNode("//a[@class='text-white inline-flex items-center justify-center rounded-[5px] py-2 px-4 text-center text-base font-bold hover:text-white mb-2']");
        if (downloadLinkNode is null)
        {
            downloadLinkNode = soup.SelectSingleNodeOrThrow("//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg flex-1']");
        }

        var downloadLink = downloadLinkNode.GetHref();
        soup = await Soupify(downloadLink, delay: ParseDelay, cancellationToken: cancellationToken);
        var link = soup.SelectSingleNodeOrThrow("//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg']").GetHref();
        return link;
    }
}