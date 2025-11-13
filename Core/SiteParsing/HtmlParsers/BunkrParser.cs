using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BunkrParser : ParameterizedHtmlParser
{
    private const int ParseDelay = 500;

    public BunkrParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                       FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for bunkr.si and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(string url)
    {
        if (url != "")
        {
            CurrentUrl = url;
        }

        var soup = await Soupify();
        var notFound = soup.SelectSingleNode("//h1[@class='text-3xl font-bold']");
        if (notFound is not null)
        {
            Log.Warning("Page not found: {CurrentUrl}", CurrentUrl);
            return RipInfo.Empty;
        }

        string dirName;
        if (url == "")
        {
            var dirNameNode = soup.SelectSingleNode("//h1[@class='text-[24px] font-bold text-dark dark:text-white']");
            if (dirNameNode is null)
            {
                dirNameNode = soup.SelectSingleNodeOrThrow("//h1[@class='truncate']");
            }

            dirName = dirNameNode.InnerText;
        }
        else
        {
            dirName = "internal-use";
        }

        List<StringImageLinkWrapper> images = [];
        StringImageLinkWrapper? link = null;
        if (CurrentUrl.Contains("/a/"))
        {
            var grid = soup.SelectSingleNode("//div[@class='grid-images']");
            if (grid is not null)
            {
                goto GridInitializationEnd;
            }

            grid = soup.SelectSingleNodeOrThrow(
                "//div[@class='grid gap-4 grid-cols-repeat [--size:11rem] lg:[--size:14rem] grid-images']");

            GridInitializationEnd:
            var imagePosts = grid.SelectNodesOrThrow(".//a");
            foreach (var (i, post) in imagePosts.Enumerate())
            {
                var href = post.GetHref();
                if (href[0] == '/')
                {
                    href = $"https://bunkr.cr{href}";
                }

                Log.Debug("Post {index} of {total}: {href}", i + 1, imagePosts.Count, href);
                const int maxRetries = 4;
                for (var j = 0; j < maxRetries; j++)
                {
                    try
                    {
                        soup = await Soupify(href, delay: ParseDelay);
                        if (Driver.Title == "502 Bad Gateway")
                        {
                            Driver.Refresh();
                            soup = await Soupify(href, delay: ParseDelay * 2);
                        }

                        break;
                    }
                    catch (WebDriverException e) when (e.Message.StartsWith(
                                                           "The HTTP request to the remote WebDriver server for URL"))
                    {
                        if (j == maxRetries - 1)
                        {
                            throw new RipperException("Failed to load page", e);
                        }

                        Log.Debug("Failed to load page, retrying...");
                        await Task.Delay(ParseDelay * 4);
                        WebDriver.RegenerateDriver();
                    }
                }

                if (CurrentUrl.Contains("/i/"))
                {
                    link = GetImageLink(soup);
                }
                else if (CurrentUrl.Contains("/v/") || CurrentUrl.Contains("/f/"))
                {
                    link = await GetVideoLink(soup);
                }
                else if (CurrentUrl.Contains("/d/"))
                {
                    link = await GetDownloadLink(soup);
                }
                else
                {
                    Log.Warning("Unknown type: {CurrentUrl}", CurrentUrl);
                }

                if (link is not null)
                {
                    images.Add(link.Value);
                    link = null;
                }
            }
        }
        else if (CurrentUrl.Contains("/i/"))
        {
            link = GetImageLink(soup);
        }
        else if (CurrentUrl.Contains("/v/") || CurrentUrl.Contains("/f/"))
        {
            link = await GetVideoLink(soup);
        }
        else if (CurrentUrl.Contains("/d/"))
        {
            link = await GetDownloadLink(soup);
        }
        else
        {
            Log.Warning("Unknown type: {CurrentUrl}", CurrentUrl);
        }

        if (link is not null)
        {
            images.Add(link.Value);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }


    private async Task<ImageLink> GetVideoLink(HtmlNode soup)
    {
        var videoDownloadNode = soup.SelectSingleNode("//a[@id='czmDownloadz']");
        string videoDownload;
        if (videoDownloadNode is not null)
        {
            videoDownload = videoDownloadNode.GetHref();
        }
        else
        {
            videoDownload = soup
                           .SelectSingleNodeOrThrow(
                                "//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold flex-1 ic-download-01 ic-before before:text-lg']")
                           .GetHref();
        }

        soup = await Soupify(videoDownload, xpath: "//main//video", delay: ParseDelay);
        var video = soup.SelectSingleNode("//main//video");

        var downloadButton = soup.SelectSingleNodeOrThrow(
            "//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg']");
        var downloadUrl = downloadButton.GetHref();
        var filename = video is not null ? video.GetSrc().Split("/")[^1] : downloadUrl.Split("/")[^1];
        var videoLink = new ImageLink(downloadUrl, FilenameScheme, 0, filename: filename);

        return videoLink;
    }

    private static string GetImageLink(HtmlNode soup)
    {
        var img = soup.SelectSingleNodeOrThrow("//main//img");
        return img.GetSrc();
    }

    private async Task<string> GetDownloadLink(HtmlNode soup)
    {
        var downloadLinkNode =
            soup.SelectSingleNode(
                "//a[@class='text-white inline-flex items-center justify-center rounded-[5px] py-2 px-4 text-center text-base font-bold hover:text-white mb-2']");
        if (downloadLinkNode is null)
        {
            downloadLinkNode =
                soup.SelectSingleNodeOrThrow(
                    "//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg flex-1']");
        }

        var downloadLink = downloadLinkNode.GetHref();
        soup = await Soupify(downloadLink, delay: ParseDelay);
        var link = soup
                  .SelectSingleNodeOrThrow(
                       "//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg']")
                  .GetHref();

        return link;
    }
}