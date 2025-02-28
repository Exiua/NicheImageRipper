using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using HtmlAgilityPack;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BunkrParser : ParameterizedHtmlParser
{
    public BunkrParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Parses the html for bunkr.si and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(string url)
    {
        const int parseDelay = 500;
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
                dirNameNode = soup.SelectSingleNode("//h1[@class='truncate']");
            }
            
            dirName = dirNameNode.InnerText;
        }
        else
        {
            dirName = "internal-use";
        }

        List<StringImageLinkWrapper> images = [];
        if (CurrentUrl.Contains("/a/"))
        {
            var grid = soup.SelectSingleNode("//div[@class='grid-images']");
            if (grid is not null)
            {
                goto GridInitializationEnd;
            }
            
            grid = soup.SelectSingleNode("//div[@class='grid gap-4 grid-cols-repeat [--size:11rem] lg:[--size:14rem] grid-images']");

            GridInitializationEnd:
            var imagePosts = grid.SelectNodes(".//a");
            foreach (var (i, post) in imagePosts.Enumerate())
            {
                var href = post.GetHref();
                Log.Debug("Post {index} of {total}: {href}", i + 1, imagePosts.Count, href);
                soup = await Soupify(href, delay: parseDelay);
                if (Driver.Title == "502 Bad Gateway")
                {
                    Driver.Refresh();
                    soup = await Soupify(href, delay: parseDelay * 2);
                }
                
                if (CurrentUrl.Contains("/i/"))
                {
                    GetImageLink();
                }
                else if(CurrentUrl.Contains("/v/") || CurrentUrl.Contains("/f/"))
                {
                    await GetVideoLink(soup);
                }
                else if (CurrentUrl.Contains("/d/"))
                {
                    await GetDownloadLink(soup);
                }
                else
                {
                    Log.Warning("Unknown type: {CurrentUrl}", CurrentUrl);
                }
            }
        }
        else if (CurrentUrl.Contains("/i/"))
        {
            GetImageLink();
        }
        else if(CurrentUrl.Contains("/v/") || CurrentUrl.Contains("/f/"))
        {
            await GetVideoLink(soup);
        }
        else if (CurrentUrl.Contains("/d/"))
        {
            await GetDownloadLink(soup);
        }
        else
        {
            Log.Warning("Unknown type: {CurrentUrl}", CurrentUrl);
        }

        return new RipInfo(images, dirName, FilenameScheme);

        // ReSharper disable once VariableHidesOuterVariable
        async Task GetVideoLink(HtmlNode soup)
        {
            var videoDownloadNode = soup.SelectSingleNode("//a[@id='czmDownloadz']");
            string videoDownload;
            if (videoDownloadNode is not null)
            {
                videoDownload = videoDownloadNode.GetHref();
            }
            else
            {
                videoDownload = soup.SelectSingleNode("//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold flex-1 ic-download-01 ic-before before:text-lg']")
                                    .GetHref();
            }
            soup = await Soupify(videoDownload, xpath: "//main//video", delay: parseDelay);
            var video = soup.SelectSingleNode("//main//video");
                    
            var downloadButton = soup.SelectSingleNode(
                "//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg']");
            var downloadUrl = downloadButton.GetHref();
            var filename = video is not null ? video.GetSrc().Split("/")[^1] : downloadUrl.Split("/")[^1];
            var videoLink = new ImageLink(downloadUrl, FilenameScheme, 0, filename: filename);
            images.Add(videoLink);
        }

        void GetImageLink()
        {
            var img = soup.SelectSingleNode("//main//img");
            images.Add(img.GetSrc());
        }

        // ReSharper disable once VariableHidesOuterVariable
        async Task GetDownloadLink(HtmlNode soup)
        {
            var downloadLinkNode = soup.SelectSingleNode("//a[@class='text-white inline-flex items-center justify-center rounded-[5px] py-2 px-4 text-center text-base font-bold hover:text-white mb-2']");
            if (downloadLinkNode is null)
            {
                downloadLinkNode = soup.SelectSingleNode("//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg flex-1']");
            }
                    
            var downloadLink = downloadLinkNode.GetHref();
            soup = await Soupify(downloadLink, delay: parseDelay);
            var link = soup.SelectSingleNode("//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg']")
                           .GetHref();
            images.Add(link);
        }
    }
}