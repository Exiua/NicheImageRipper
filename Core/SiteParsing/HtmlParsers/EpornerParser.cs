using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.SiteParsing.HtmlParserEnums;
using HtmlAgilityPack;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EpornerParser : HtmlParser
{
    public EpornerParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for eporner.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        const string domainUrl = "https://www.eporner.com";
        
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 250
        };
        
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
    
        string dirName;
        var images = new List<StringImageLinkWrapper>();
        
        if (CurrentUrl.Contains("/profile/"))
        {
            var baseUrl = CurrentUrl.Split("/")[..5].Join("/");
            dirName = soup.SelectSingleNode("//div[@id='pprofiletopinfo']//h1").InnerText;
            var headers = soup.SelectSingleNode("//div[@id='pnavtop']").SelectNodes("./a");
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
                await ExtractFromListView(baseUrl, "uploaded-videos", "streameventsday showAll", posts);
                // if (profileSections.HasFlag(EpornerProfileSections.Playlists))
                // {
                //     await ExtractFromListView(baseUrl, "playlists", "streameventsday showAll", posts);
                // }
                
                foreach (var post in posts)
                {
                    Log.Information("Parsing video: {post}", post);
                    soup = await Soupify(post, lazyLoadArgs: lazyLoadArgs);
                    var downloadLink = ExtractVideoDownloadLink(soup);
                    images.Add(downloadLink);
                }
            }
            
            #endregion
    
            #region Extract Images
    
            posts.Clear();
            if (profileSections.HasFlag(EpornerProfileSections.Images))
            {
                await ExtractFromListView(baseUrl, "uploaded-pics", "streameventsday photosgrid showAll", posts);
                foreach (var post in posts)
                {
                    Log.Information("Parsing gallery: {post}", post);
                    soup = await Soupify(post, lazyLoadArgs: lazyLoadArgs);
                    var postImages = soup.SelectSingleNode("//div[@class='photosgrid gallerygrid']")
                                            .SelectNodes("./div")
                                            .Select(div => div.SelectSingleNode(".//img").GetSrc())
                                            .Select(ExtractFullImageLink)
                                            .ToStringImageLinks();
                    images.AddRange(postImages);
                }
            }
            
            #endregion
        
        }
        else if (CurrentUrl.Contains("/video-") || CurrentUrl.Contains("/hd-porn/"))
        {
            dirName = soup.SelectSingleNode("//div[@id='video-info']/h1").InnerText;
            var downloadLink = ExtractVideoDownloadLink(soup);
            images.Add(downloadLink);
        }
        else
        {
            var e = new RipperException("Unknown Eporner URL type");
            Log.Error(e, "Unknown Eporner URL type: {CurrentUrl}", CurrentUrl);
            throw e;
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    
        // ReSharper disable once VariableHidesOuterVariable
        string ExtractVideoDownloadLink(HtmlNode soup)
        {
            var downloads = soup.SelectSingleNode("//div[@id='hd-porn-dload']")
                                .SelectNodes(".//span");
            string downloadLink;
            if (downloads.Count > 1)
            {
                var secondLast = downloads[^2];
                if (secondLast.GetAttributeValue("class") == "download-av1")
                {
                    downloadLink = secondLast.SelectSingleNode("./a").GetHref();
                }
                else
                {
                    var last = downloads[^1];
                    downloadLink = last.SelectSingleNode("./a").GetHref();
                }
            }
            else
            {
                var last = downloads[^1];
                downloadLink = last.SelectSingleNode("./a").GetHref();
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
    
        async Task ExtractFromListView(string baseUrl, string section, string divClass, List<string> posts)
        {
            // ReSharper disable once VariableHidesOuterVariable
            var soup = await Soupify($"{baseUrl}/{section}/", lazyLoadArgs: lazyLoadArgs);
            while(true)
            {
                var links = soup.SelectSingleNode($"//div[@class='{divClass}']")
                                .SelectNodes("./div[contains(@class, 'mb')]")
                                .Select(div => domainUrl + div.SelectSingleNode(".//a").GetHref());
                posts.AddRange(links);
                var nextPage = soup.SelectSingleNode("//a[@class='nmnext']");
                if (nextPage is null)
                {
                    break;
                }
                    
                var nextPageUrl = nextPage.GetHref();
                soup = await Soupify($"{domainUrl}{nextPageUrl}");
            }
        }
    }
}
