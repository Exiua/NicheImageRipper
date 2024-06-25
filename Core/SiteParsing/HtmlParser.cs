using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Utility;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;

namespace Core.SiteParsing;

public partial class HtmlParser
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0";

    private const string Protocol = "https:";

    private static readonly string[] ExternalSites =
        ["drive.google.com", "mega.nz", "mediafire.com", "sendvid.com", "dropbox.com"];

    private static readonly string[] ParsableSites = ["drive.google.com", "mega.nz", "sendvid.com", "dropbox.com"];

    private static bool LoggedIn;

    private static FirefoxDriver Driver { get; set; } = new(InitializeOptions(""));

    public bool Interrupted { get; set; }
    private string SiteName { get; set; }
    public float SleepTime { get; set; }
    public float Jitter { get; set; }
    private string GivenUrl { get; set; }
    private FilenameScheme FilenameScheme { get; set; }
    private Dictionary<string, string> RequestHeaders { get; set; }

    private static string CurrentUrl
    {
        get => Driver.Url;
        set => Driver.Url = value;
    }

    public HtmlParser(Dictionary<string, string> requestHeaders, string siteName = "",
                      FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        //var options = InitializeOptions(siteName);
        //Driver = new FirefoxDriver(options);
        RequestHeaders = requestHeaders;
        FilenameScheme = filenameScheme;
        Interrupted = false;
        SiteName = siteName;
        SleepTime = 0.2f;
        Jitter = 0.5f;
        GivenUrl = "";
    }

    public async Task<RipInfo> ParseSite(string url)
    {
        if (File.Exists("partial.json"))
        {
            var saveData = ReadPartialSave();
            if (saveData.TryGetValue(url, out var value))
            {
                RequestHeaders["cookie"] = value.Cookies;
                RequestHeaders["referer"] = value.Referer;
                Interrupted = true;
                return value.RipInfo;
            }
        }

        url = url.Replace("members.", "www.");
        GivenUrl = url;
        CurrentUrl = url;
        (SiteName, SleepTime) = UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        var siteParser = GetParser(SiteName);
        try
        {
            var siteInfo = await siteParser();
            WritePartialSave(siteInfo, url);
            //pickle.dump(self.driver.get_cookies(), open("cookies.pkl", "wb"))
            return siteInfo;
        }
        catch
        {
            Print(CurrentUrl);
            throw;
        }
    }

    private Func<Task<RipInfo>> GetParser(string siteName)
    {
        return siteName switch
        {
            "imhentai" => ImhentaiParse,
            "kemono" => KemonoParse,
            "coomer" => CoomerParse,
            "sankakucomplex" => SankakuComplexParse,
            "omegascans" => OmegaScansParse,
            "redgifs" => RedGifsParse,
            "rule34" => Rule34Parse,
            "gelbooru" => GelbooruParse,
            "danbooru" => DanbooruParse,
            "google" => GoogleParse,
            "dropbox" => DropboxParse,
            "imgur" => ImgurParse,
            "newgrounds" => NewgroundsParse,
            "wnacg" => WnacgParse,
            "arca" => ArcaParse,
            "babecentrum" => BabeCentrumParse,
            "babeimpact" => BabeImpactParse,
            "babeuniversum" => BabeUniversumParse,
            "babesandbitches" => BabesAndBitchesParse,
            "babesandgirls" => BabesAndGirlsParse,
            "babesaround" => BabesAroundParse,
            "babesbang" => BabesBangParse,
            "babesinporn" => BabesInPornParse,
            "babesmachine" => BabesMachineParse,
            "bestprettygirl" => BestPrettyGirlParse,
            "bitchesgirls" => BitchesGirlsParse,
            "bunkr" => BunkrParse,
            "buondua" => BuonduaParse,
            "bustybloom" => BustyBloomParse,
            _ => throw new Exception("Site not supported/implemented")
        };
    }

    private static Dictionary<string, PartialSaveEntry> ReadPartialSave()
    {
        return JsonUtility.Deserialize<Dictionary<string, PartialSaveEntry>>("partial.json")!;
    }

    private void WritePartialSave(RipInfo ripInfo, string url)
    {
        var partialSaveEntry = new PartialSaveEntry
        {
            Cookies = RequestHeaders["cookie"],
            Referer = RequestHeaders["referer"],
            RipInfo = ripInfo
        };
        var partialSave = new Dictionary<string, PartialSaveEntry> { { url, partialSaveEntry } };
        JsonUtility.Serialize("partial.json", partialSave);
    }

    private static FirefoxOptions InitializeOptions(string siteName)
    {
        var options = new FirefoxOptions();
        if (siteName != "v2ph" || LoggedIn || siteName == "debug")
        {
            options.AddArgument("--headless");
        }

        options.SetPreference("general.useragent.override", UserAgent);
        options.SetPreference("media.volume_scale", "0.0");
        
        return options;
    }

    private Task<bool> SiteLogin()
    {
        return SiteName switch
        {
            "newgrounds" => NewgroundsLogin(),
            _ => throw new Exception("Site authentication not implemented")
        };
    }

    private static async Task<bool> NewgroundsLogin()
    {
        var currUrl = CurrentUrl;
        var (username, password) = Config.Instance.Logins["Newgrounds"];
        CurrentUrl = "https://newgrounds.com/passport";
        Driver.FindElement(By.XPath("//input[@name='username']"))
              .SendKeys(username);
        Driver.FindElement(By.XPath("//input[@name='password']"))
              .SendKeys(password);
        Driver.FindElement(By.XPath("//button[@name='login']")).Click();
        while (CurrentUrl != "https://www.newgrounds.com/social")
        {
            await Task.Delay(1000);
        }

        CurrentUrl = currUrl;
        return true;
    }

    #region Site Parsers

    #region Generic Site Parsers

    /// <summary>
    ///     Parses the html for kemono.su and coomer.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name="domainUrl">The domain url of the site</param>
    /// <returns></returns>
    private async Task<RipInfo> DotPartyParse(string domainUrl)
    {
        var cookies = Driver.Manage().Cookies.AllCookies;
        var cookieStr = cookies.Aggregate("", (current, c) => current + $"{c.Name}={c.Value};");
        RequestHeaders["cookie"] = cookieStr;
        var baseUrl = CurrentUrl;
        var urlSplit = baseUrl.Split("/");
        var sourceSite = urlSplit[3];
        baseUrl = string.Join("/", urlSplit[..6]).Split("?")[0];
        var pageUrl = CurrentUrl.Split("?")[0];
        await Task.Delay(5000);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@id='user-header__info-top']")
                          .SelectSingleNode("//span[@itemprop='name']").InnerText;
        dirName = $"{dirName} - ({sourceSite})";

        #region Get All Posts

        var page = 0;
        var imageLinks = new List<string>();
        var origUrl = CurrentUrl;
        while (true)
        {
            page += 1;
            Print($"Parsing page {page}");
            var imageList = soup.SelectSingleNode("//div[@class='card-list__items']").SelectNodes("//article");
            foreach (var image in imageList)
            {
                var id = image.GetAttributeValue("data-id", "");
                imageLinks.Add($"{baseUrl}/post/{id}");
            }

            var nextUrl = $"{pageUrl}?o={page * 50}";
            Print(nextUrl);
            soup = await Soupify(nextUrl);
            if (soup.SelectSingleNode("//h2[@class='site-section__subheading']") is not null || CurrentUrl == origUrl)
            {
                break;
            }
        }

        #endregion

        #region Parse All Posts

        var images = new List<string>();
        var externalLinks = CreateExternalLinkDict();
        var numPosts = imageLinks.Count;
        string[] attachmentExtensions =
            [".zip", ".rar", ".mp4", ".webm", ".psd", ".clip", ".m4v", ".7z", ".jpg", ".png", ".webp"];

        foreach (var (i, link) in imageLinks.Enumerate())
        {
            Print($"Parsing post {i + 1} of {numPosts}");
            Print(link);
            soup = await Soupify(link);
            var links = soup.SelectNodes("//a").GetHrefs();
            var possibleLinksP = soup.SelectNodes("//p");
            var possibleLinks = possibleLinksP.Select(p => p.InnerText).ToList();
            var possibleLinksDiv = soup.SelectNodes("//div");
            possibleLinks.AddRange(possibleLinksDiv.Select(d => d.InnerText));
            var extLinks = ExtractExternalUrls(links);
            foreach (var site in extLinks.Keys)
            {
                externalLinks[site].AddRange(extLinks[site]);
            }

            extLinks = ExtractPossibleExternalUrls(possibleLinks);
            foreach (var site in extLinks.Keys)
            {
                externalLinks[site].AddRange(extLinks[site]);
            }

            var attachments = new List<string>();
            foreach (var l in links)
            {
                if (!attachmentExtensions.AnyIn(l))
                {
                    continue;
                }

                var attachment = l.Contains(domainUrl) || l.Contains("http") /* Includes https */ ? l : domainUrl + l;
                attachments.Add(attachment);
            }

            images.AddRange(attachments);
            foreach (var site in ParsableSites)
            {
                images.AddRange(externalLinks[site]);
            }

            var imageListContainer = soup.SelectSingleNode("//div[@class='post__files']");
            if (imageListContainer is null)
            {
                continue;
            }

            var imageList = imageListContainer.SelectNodes("//a[@class='fileThumb image-link']");
            var imageListLinks = imageList.GetHrefs();
            images.AddRange(imageListLinks);
        }

        #endregion

        foreach (var site in ExternalSites)
        {
            externalLinks[site] = externalLinks[site].RemoveDuplicates();
        }

        SaveExternalLinks(externalLinks);
        images = images.RemoveDuplicates();
        var oldLinks = images;
        var stringLinks = new List<StringImageLinkWrapper>();
        foreach (var link in oldLinks)
        {
            if (!link.Contains("dropbox.com/"))
            {
                stringLinks.Add(link);
            }
            else
            {
                var ripInfo = await DropboxParse(link);
                stringLinks.AddRange(ripInfo.Urls.Select(x => new StringImageLinkWrapper(x)));
            }
        }

        // Remove duplicates
        var seen = new HashSet<string>();
        var unique = new List<StringImageLinkWrapper>();
        foreach (var link in stringLinks)
        {
            var domainName = new Uri(link).Host.Split('.')[0];
            var linkStr = link.ToString();
            if (!linkStr.Contains(domainName)) // Only native links get duplicated
            {
                unique.Add(link);
                continue;
            }

            var fileName = linkStr.Split("/")[^1];
            if (seen.Add(fileName))
            {
                unique.Add(link);
            }
        }

        return new RipInfo(unique, dirName, FilenameScheme);
    }

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericBabesHtmlParser(string dirNameXpath, string imageContainerXpath)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode(dirNameXpath)
                          .InnerText;
        var images = soup.SelectNodes(imageContainerXpath)
                         .SelectMany(im => im.SelectNodes(".//img"))
                         .Select(img => Protocol + img.GetSrc().Remove("tn_"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericHtmlParser()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//img[@title='Click To Enlarge!']")
                          .GetAttributeValue("alt")
                          .Split(" ")
                          .TakeWhile(s => s != "-")
                          .Join(" ");
        var images = soup.SelectNodes("//div[@class='gallery_thumb']")
                            .Select(img => Protocol + img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /*
     * def __generic_html_parser_2(self):
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("img", title="Click To Enlarge!").get("alt").split()
        for i in range(len(dir_name)):
            if dir_name[i] == '-':
                del dir_name[i:]
                break
        dir_name = " ".join(dir_name)
        images = soup.find_all("div", class_="gallery_thumb")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)
     */

    #endregion

    // TODO: Test that method works
    /// <summary>
    ///     Parses the html for agirlpic.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> AGirlPicParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='entry-title'").InnerText;
        var baseUrl = CurrentUrl;
        var numPages = soup.SelectSingleNode("//div[@class='page-links']")
                          .SelectNodes("./a")
                          .Count + 1;
        var images = new List<StringImageLinkWrapper>();
        for (var i = 1; i <= numPages; i++)
        {
            var tags = soup.SelectSingleNode("//div[@class='entry-content clear']")
                           .SelectNodes("./div[@class='separator']")
                           .ToList();
            foreach (var imgTags in tags.Select(tag => tag.SelectNodes(".//img")))
            {
                images.AddRange(imgTags.Select(img => img.GetSrc()).Select(dummy => (StringImageLinkWrapper)dummy));
            }

            if (i != numPages)
            {
                soup = await Soupify($"{baseUrl}{i + 1}/");
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for arca.live and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ArcaParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='title']").InnerText;
        var mainTag = soup.SelectSingleNode("//div[@class='fr-view article-content']");
        var imageList = mainTag.SelectNodes(".//img").GetSrcs();
        var images = imageList
                    .Select(image => image.Split("?")[0] + "?type=orig") // Remove query string and add type=orig
                    .Select(img => !img.Contains("https:") ? "https:" + img : img) // Add protocol if missing
                    .Select(dummy => (StringImageLinkWrapper)dummy) // Convert to StringImageLinkWrapper
                    .ToList();
        var videoList = mainTag.SelectNodes(".//video").GetSrcs();
        var videos = videoList
                    .Select(video => !video.Contains("https:") ? "https:" + video : video)
                    .Select(dummy => (StringImageLinkWrapper)dummy)
                    .ToList();
        images.AddRange(videos);
        
        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for artstation.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ArtstationParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='artist-name']").InnerText;
        var username = CurrentUrl.Split("/")[3];
        var cacheScript = soup.SelectSingleNode("//div[@class='wrapper-main']").SelectNodes(".//script")[1].InnerText;
        
        #region Id Extraction
        
        var start = cacheScript.IndexOf("quick.json", StringComparison.Ordinal);
        var end = cacheScript.LastIndexOf(");", StringComparison.Ordinal);
        var jsonData = cacheScript[(start + 14)..(end - 1)].Replace("\n", "").Replace('\"', '"');
        var json = JsonSerializer.Deserialize<JsonNode>(jsonData);
        var userId = json!["id"]!.Deserialize<string>()!;
        var userName = json["full_name"]!.Deserialize<string>()!;
        
        #endregion
        
        #region Get Posts
        
        var total = 1;
        var pageCount = 1;
        var firstIter = true;
        var posts = new List<string>();
        var client = new HttpClient();

        while (total > 0)
        {
            Console.WriteLine(pageCount);
            var url = $"https://www.artstation.com/users/{username}/projects.json?page={pageCount}";
            Console.WriteLine(url);
            var response = await client.GetAsync(url);
            var responseData = await response.Content.ReadFromJsonAsync<JsonNode>();
            var data = responseData!["data"]!.AsArray();
            foreach (var d in data)
            {
                var link = d!["permalink"]!.Deserialize<string>()!.Split("/")[4];
                posts.Add(link);
            }
            
            if (firstIter)
            {
                total = responseData["total_count"]!.Deserialize<int>() - data.Count;
                firstIter = false;
            }
            else
            {
                total -= data.Count;
            }
            
            pageCount += 1;
            await Task.Delay(100);
        }
        
        #endregion
        
        #region Get Media Links
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in posts)
        {
            var url = $"https://www.artstation.com/projects/{post}.json";
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(url);
            }
            catch (HttpRequestException)
            {
                await Task.Delay(5000);
                response = await client.GetAsync(url);
            }
            
            var responseData = await response.Content.ReadFromJsonAsync<JsonNode>();
            var assets = responseData!["assets"]!.AsArray();
            var urls = assets.Select(asset => asset!["image_url"]!.Deserialize<string>()!.Replace("/large/", "/4k/"));
            images.AddRange(urls.Select(url => (StringImageLinkWrapper)url));
        }
        
        #endregion
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babecentrum.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabeCentrumParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='pageHeading']")
                          .SelectNodes(".//cufontext")
                          .Select(w => w.InnerText)
                          .Join(" ")
                          .Trim();
        var images = soup.SelectSingleNode("//table")
                            .SelectNodes(".//img")
                            .Select(img => Protocol + img.GetAttributeValue("src", "").Remove("tn_"))
                            .Select(dummy => (StringImageLinkWrapper)dummy)   
                            .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babeimpact.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabeImpactParse()
    {
        var soup = await Soupify();
        var title = soup.SelectSingleNode("//h1[@class='blockheader pink center lowercase']").InnerText;
        var sponsor = soup.SelectSingleNode("//div[@class='c']")
                         .SelectNodes(".//a")[1]
                         .InnerText
                         .Trim();
        sponsor = $"({sponsor})";
        var dirName = $"{sponsor} {title}";
        var tags = soup.SelectNodes("//div[@class='list gallery']");
        var tagList = new List<HtmlNode>();
        foreach (var tag in tags)
        {
            tagList.AddRange(tag.SelectNodes(".//div[@class='item']"));
        }

        var images = new List<StringImageLinkWrapper>();
        var imageList = tagList.Select(tag => tag.SelectSingleNode(".//a")).Select(anchor => $"https://babeimpact.com{anchor.GetHref()}").ToList();
        foreach (var image in imageList)
        {
            soup = await Soupify(image);
            var img = soup.SelectSingleNode("//div[@class='image-wrapper']")
                         .SelectSingleNode(".//img")
                         .GetSrc();
            images.Add((StringImageLinkWrapper)(Protocol + img));
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babeuniversum.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabeUniversumParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='title']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@class='three-column']")
                         .SelectNodes(".//div[@class='thumbnail']")
                         .Select(img => Protocol + img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babesandbitches.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabesAndBitchesParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@id='title']")
                          .InnerText
                          .Split("picture")[0]
                          .Trim();
        var images = soup.SelectNodes("//a[@class='gallery-thumb']")
                            .Select(img => Protocol + img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babesandgirls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabesAndGirlsParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='title']")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@class='block-post album-item']")
                            .SelectNodes(".//a[@class='item-post']")
                            .Select(img => Protocol + img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babesaround.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> BabesAroundParse()
    {
        return GenericBabesHtmlParser("//section[@class='outer-section']//h2", "//div[@class='lightgallery thumbs quadruple fivefold']");
    }

    /// <summary>
    ///     Parses the html for babesbang.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> BabesBangParse()
    {
        return GenericBabesHtmlParser("//div[@class='main-title']", "//div[@class='gal-block']");
    }

    /// <summary>
    ///     Parses the html for babesinporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> BabesInPornParse()
    {
        return GenericBabesHtmlParser("//h1[@class='blockheader pink center lowercase']", "//div[@class='list gallery']");
    }

    /// <summary>
    ///     Parses the html for babesmachine.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabesMachineParse()
    {
        var soup = await Soupify();
        var gallery = soup.SelectSingleNode("//div[@id='gallery']");
        var dirName = gallery.SelectSingleNode(".//h2")
                             .SelectSingleNode(".//a")
                             .InnerText;
        var images = gallery.SelectSingleNode(".//table").SelectNodes(".//tr")
                            .Select(img => img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                            .Select(img => Protocol + img)
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for bestprettygirl.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BestPrettyGirlParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='elementor-heading-title elementor-size-large']").InnerText;
        var images = soup.SelectNodes("//img[@class='aligncenter size-full']")
                         .Select(img => img.GetSrc())
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for bitchesgirls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BitchesGirlsParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='album-name']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var baseUrl = CurrentUrl;
        if (baseUrl[^1] != '/')
        {
            baseUrl += "/";
        }
        
        var page = 1;
        while (true)
        {
            if (page != 1)
            {
                soup = await Soupify($"{baseUrl}{page}");
            }

            var posts = soup.SelectSingleNode("//div[@class='albumgrid']")
                            .SelectNodes("./a[@class='post-container']")
                            .Select(post => post.GetHref())
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();
            images.AddRange(posts);
            var loadBtn = soup.SelectSingleNode("//a[@id='loadMore']");
            if (loadBtn is not null)
            {
                page += 1;
            }
            else
            {
                break;
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for bunkr.si and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> BunkrParse()
    {
        return BunkrParse("");
    }
    
    /// <summary>
    ///     Parses the html for bunkr.si and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BunkrParse(string url)
    {
        if (url != "")
        {
            CurrentUrl = url;
        }
        
        var soup = await Soupify();
        string dirName;
        if (url == "")
        {
            dirName = soup.SelectSingleNode("//h1[@class='text-[24px] font-bold text-dark dark:text-white']")
                          .InnerText;
        }
        else
        {
            dirName = "internal-use";
        }

        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/a/"))
        {
            images = [];
            var imagePosts = soup.SelectSingleNode("//div[@class='grid-images']")
                                 .SelectNodes(".//a");
            foreach (var post in imagePosts)
            {
                var href = post.GetHref();
                var ext = href.Split('.')[^1];
                var link = ext switch
                {
                    "mp4" or "webm" or "avi" or "rar" or "zip" or "7z" => $"https://fries.bunkrr.su{href}".Replace("/d/", "/"),
                    _ => post.SelectSingleNode(".//img")
                            .GetSrc()
                            .Replace("/thumbs/", "/")
                            .Replace(".png", $".{ext}")
                };
                images.Add((StringImageLinkWrapper)link);
            }
        }
        else
        {
            var anchor =
                soup.SelectSingleNode(
                    "//a[@class='text-white inline-flex items-center justify-center rounded-[5px] py-2 px-4 text-center text-base font-bold hover:text-white mb-2']");
            var link = anchor.GetHref();
            images = [(StringImageLinkWrapper)link];
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for buondua.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BuonduaParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNode("//div[@class='article-header']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var dirNameSplit = dirName.Split("(");
        if (dirName.Contains("pictures") || dirName.Contains("photos"))
        {
            dirName = dirNameSplit[..^1].Join("(");
        }
        
        var pages = soup.SelectSingleNode("//div[@class='pagination-list']")
                       .SelectNodes(".//span")
                       .Count;
        var currUrl = CurrentUrl.Replace("?page=1", "");
        
        var images = new List<StringImageLinkWrapper>();
        for (var i = 0; i < pages; i++)
        {
            var imageList = soup.SelectSingleNode("//div[@class='article-fulltext']")
                               .SelectNodes(".//img")
                               .Select(img => img.GetSrc())
                               .Select(dummy => (StringImageLinkWrapper)dummy)
                               .ToList();
            images.AddRange(imageList);
            if (i >= pages - 1)
            {
                continue;
            }

            var nextPage = $"{currUrl}?page={i + 2}";
            CurrentUrl = nextPage;
            soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
            {
                ScrollBy = true
            });
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for bustybloom.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> BustyBloomParse()
    {
        return GenericHtmlParser();
    }
    
    /// <summary>
    ///     Parses the html for coomer.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CoomerParse()
    {
        return await DotPartyParse("https://coomer.su");
    }

    /// <summary>
    ///     Parses the html for danbooru.donmai.us and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private async Task<RipInfo> DanbooruParse()
    {
        var tags = Rule34Regex().Match(CurrentUrl).Groups[1].Value;
        tags = Uri.UnescapeDataString(tags);
        var dirName = "[Danbooru] " + tags.Replace("+", " ").Replace("tags=", "");
        var images = new List<StringImageLinkWrapper>();
        var session = new HttpClient();
        session.DefaultRequestHeaders.Add("User-Agent", "NicheImageRipper");
        var response = await session.GetAsync($"https://danbooru.donmai.us/posts.json?{tags}");
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var data = json!.AsArray();
        var i = 0;
        while (data.Count != 0)
        {
            var urls = data.Select(post => post!["file_url"]!.Deserialize<string>()!);
            images.AddRange(urls.Select(url => (StringImageLinkWrapper)url));
            response = await session.GetAsync($"https://danbooru.donmai.us/posts.json?{tags}&page={i}");
            json = await response.Content.ReadFromJsonAsync<JsonNode>();
            data = json!.AsArray();
            i += 1;
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    private async Task<RipInfo> DropboxParse()
    {
        return await DropboxParse("");
    }

    /// <summary>
    ///     Parses the html for dropbox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name="dropboxUrl"></param>
    /// <returns></returns>
    private async Task<RipInfo> DropboxParse(string dropboxUrl)
    {
        var internalUse = false;
        if (!string.IsNullOrEmpty(dropboxUrl))
        {
            if (dropboxUrl.Contains("/scl/fi/"))
            {
                dropboxUrl = dropboxUrl.Replace("dl=0", "dl=1");
                return new RipInfo([dropboxUrl], "", FilenameScheme);
            }

            CurrentUrl = dropboxUrl;
            internalUse = true;
        }

        var soup = await Soupify(xpath: "//span[@class='dig-Breadcrumb-link-text']");
        string dirName;
        if (!internalUse)
        {
            try
            {
                dirName = soup.SelectSingleNode("//span[@class='dig-Breadcrumb-link-text']").InnerText;
            }
            catch (NullReferenceException)
            {
                var deletedNotice =
                    soup.SelectSingleNode("//h2[@class='dig-Title dig-Title--size-large dig-Title--color-standard']");
                if (deletedNotice is not null)
                {
                    return new RipInfo([], "Deleted", FilenameScheme);
                }

                Print(CurrentUrl);
                throw;
            }
        }
        else
        {
            dirName = "";
        }

        if (CurrentUrl.Contains("/scl/fi/"))
        {
            return new RipInfo([CurrentUrl.Replace("dl=0", "dl=1")], "", FilenameScheme);
        }

        var images = new List<string>();
        var filenames = new List<string>();
        var postsNodes = soup.SelectSingleNode("//ol[@class='_sl-grid-body_6yqpe_26']");
        var posts = new List<string>();
        if (postsNodes is not null)
        {
            posts = postsNodes.SelectNodes("//a").GetHrefs().RemoveDuplicates();
            foreach (var post in posts)
            {
                soup = await Soupify(post, xpath: "//img[@class='_fullSizeImg_1anuf_16']");
                GetDropboxFile(soup, post, filenames, images,
                    posts); // The method modifies the posts list which is being iterated over...
            }
        }
        else
        {
            GetDropboxFile(soup, CurrentUrl, filenames, images, posts);
        }

        return new RipInfo(images.ToWrapperList(), dirName, FilenameScheme, filenames: filenames);
    }
    
    private static void GetDropboxFile(HtmlNode soup, string post, List<string> filenames, List<string> images,
                                       List<string> posts)
    {
        var filename = post.Split("/")[^1].Split("?")[0];
        filenames.Add(filename);
        var img = soup.SelectSingleNode("//img[@class='_fullSizeImg_1anuf_16']");
        try
        {
            if (img is not null)
            {
                var src = img.GetAttributeValue("src", "");
                if (src != "")
                {
                    images.Add(src);
                }
            }
            else
            {
                var vid = soup.SelectSingleNode("//video");
                if (vid is not null)
                {
                    var src = vid.SelectSingleNode("//source").GetAttributeValue("src", "");
                    if (src != "")
                    {
                        images.Add(src);
                    }
                }
                else
                {
                    var newPosts = soup
                                  .SelectSingleNode("//ol[@class='_sl-grid-body_6yqpe_26']")
                                  .SelectNodes("//a")
                                  .GetHrefs()
                                  .RemoveDuplicates();
                    posts.AddRange(newPosts);
                }
            }
        }
        catch (NullReferenceException) // AttributeError
        {
            // ignored
        }
    }

    private async Task<RipInfo> EHentaiParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[id='gn']").InnerText;
        var imageLinks = new List<string>();
        var pageCount = 1;
        while (true)
        {
            await PrintAsync($"Parsing page {pageCount}");
            var imageTags = soup.SelectNodes("//div[@class='gdt']//a").GetHrefs();
            imageLinks.AddRange(imageTags);
            var nextPage = soup.SelectSingleNode("//table[@class='ptb']").SelectNodes("//a").GetHrefs().Last();
            if (nextPage == CurrentUrl)
            {
                break;
            }
            
            await Task.Delay(5000);
            try
            {
                pageCount += 1;
                CurrentUrl = nextPage;
            }
            catch (WebDriverTimeoutException)
            {
                await PrintAsync("Timed out. Sleeping for 10 seconds before retrying...", true);
                await Task.Delay(10000);
                CurrentUrl = nextPage;
            }
            soup = await Soupify();
        }
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, link) in imageLinks.Enumerate())
        {
            await PrintAsync($"Parsing image {i + 1}/{imageLinks.Count}");
            await Task.Delay(2500);
            soup = await Soupify(link);
            var img = soup.SelectSingleNode("//img[@id='img']").GetAttributeValue("src", "");
            images.Add(img);
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for gelbooru.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GelbooruParse()
    {
        var tags = Rule34Regex().Match(CurrentUrl).Groups[1].Value;
        tags = Uri.UnescapeDataString(tags);
        var dirName = "[Gelbooru] " + tags.Replace("+", " ").Replace("tags=", "");
        var session = new HttpClient();
        var response =
            await session.GetAsync($"https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&pid=0&{tags}");
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var data = json!["post"]!.AsArray();
        var images = new List<StringImageLinkWrapper>();
        var pid = 1;
        while (true)
        {
            var urls = data.Select(post => post!["file_url"]!.Deserialize<string>()!);
            images.AddRange(urls.Select(url => (StringImageLinkWrapper)url));
            response = await session.GetAsync(
                $"https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&pid={pid}&{tags}");
            pid += 1;
            json = await response.Content.ReadFromJsonAsync<JsonNode>();
            var posts = json!["post"];
            if (posts is null)
            {
                break;
            }

            data = posts.AsArray();
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    private async Task<RipInfo> GoogleParse()
    {
        return await GoogleParse("");
    }

    /// <summary>
    ///     Query the google drive API to get file information to download
    /// </summary>
    /// <param name="gdriveUrl">The url to parse (default: CurrentUrl)</param>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GoogleParse(string gdriveUrl)
    {
        if (string.IsNullOrEmpty(gdriveUrl))
        {
            gdriveUrl = CurrentUrl;
        }

        // Actual querying happens within the RipInfo object
        return new RipInfo([gdriveUrl], "", FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for imgur.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ImgurParse()
    {
        var clientId = Config.Instance.Keys["Imgur"];
        if (clientId == "")
        {
            Print("Client Id not properly set");
            Print("Follow to generate Client Id: https://apidocs.imgur.com/#intro");
            Print("Then add Client Id to Imgur in config.json under Keys");
            throw new Exception("Client Id Not Set");
        }

        RequestHeaders["Authorization"] = "Client-ID " + clientId;
        var albumHash = CurrentUrl.Split("/")[5];
        var session = new HttpClient();
        var request = RequestHeaders.ToRequest(HttpMethod.Get, $"https://api.imgur.com/3/album/{albumHash}");
        var response = await session.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            Print("Client Id is incorrect");
            throw new Exception("Client Id Incorrect");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var jsonData = json!["data"]!.AsObject();
        var dirName = jsonData["title"]!.Deserialize<string>()!;
        var images = jsonData["images"]!.AsArray().Select(img => img!["link"]!.Deserialize<string>()!).ToList();

        return new RipInfo(images.ToWrapperList(), dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for imhentai.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ImhentaiParse()
    {
        if (!CurrentUrl.Contains("/gallery/"))
        {
            var galCode = CurrentUrl.Split("/")[4];
            CurrentUrl = $"https://imhentai.xxx/gallery/{galCode}/";
        }

        var soup = await Soupify();
        var images = soup.SelectSingleNode("//img[@class='lazy preloader']").GetAttributeValue("data-src", "");
        if (images == "")
        {
            throw new NotFoundException("Image not found");
        }

        var numPages = int.Parse(soup.SelectSingleNode("//li[@class='pages']").InnerText.Split()[1]);
        var dirName = soup.SelectSingleNode("//h1").InnerText;

        return new RipInfo([images], dirName, generate: true, numUrls: numPages);
    }

    /// <summary>
    ///     Parses the html for kemono.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private async Task<RipInfo> KemonoParse()
    {
        return await DotPartyParse("https://kemono.su");
    }

    /// <summary>
    ///     Parses the html for rule34.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> NewgroundsParse()
    {
        if (CurrentUrl.EndsWith("/art/"))
        {
            var url = CurrentUrl.Split("/")[..3];
            CurrentUrl = $"{"/".Join(url)}/art/";
        }

        DebugUtility.Print("Logging in to Newgrounds");
        await SiteLogin();
        DebugUtility.Print("Loading Newgrounds page");
        await LazyLoad(scrollBy: true);
        DebugUtility.Print("Parsing Newgrounds page");
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//a[@class='user-link']").InnerText!.Trim();
        var postYears = soup.SelectSingleNode("//div[@class='userpage-browse-content']")
                            .SelectSingleNode("./div")
                            .SelectNodes("./div")
                            .ToList();
        var posts = new List<string>();
        foreach (var postYear in postYears)
        {
            var yearPosts = postYear.SelectNodes(".//div[@class='span-1 align-center']");
            foreach (var post in yearPosts)
            {
                var postLink = post.SelectSingleNode("./a").GetAttributeValue("href", "")!;
                posts.Add(postLink);
            }
        }

        var images = new List<StringImageLinkWrapper>();
        foreach (var post in posts)
        {
            CurrentUrl = post;
            soup = await Soupify();
            var divContainer = soup.SelectSingleNode("//div[@class='ng-img-container-sync art-item-container']");
            var anchor = divContainer is null
                ? soup.SelectSingleNode("//a[@class='medium_image']")
                : divContainer.SelectSingleNode(".//a");
            var imgLink = anchor.GetAttributeValue("href", "");
            images.Add(imgLink);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for omegascans.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private async Task<RipInfo> OmegaScansParse()
    {
        await Task.Delay(5000);
        var soup = await Soupify();
        var dirName = soup
                     .SelectSingleNode(
                          "//h1[@class='text-xl md:text-3xl text-primary font-bold text-center lg:text-left']")
                     .InnerText;
        var chapterCountStr = soup.SelectSingleNode("//div[@class='space-y-2 rounded p-5 bg-foreground']")
                                  .SelectNodes("./div[@class='flex justify-between']")[3]
                                  .SelectSingleNode(".//span[@class='text-secondary line-clamp-1']").InnerText;
        var chapterCount = int.Parse(chapterCountStr.Trim().Split(' ')[0]);
        List<string> chapters = [];
        while (true)
        {
            var links = soup.SelectSingleNode("//ul[@class='grid grid-cols-1 gap-y-8']")
                            .SelectNodes("./a[@href]").GetHrefs().Select(link => $"https://omegascans.org{link}")
                            .ToList();
            chapters.AddRange(links);
            if (chapters.Count == chapterCount)
            {
                break;
            }

            Driver.FindElement(By.XPath("//nav[@class='mx-auto flex w-full justify-center gap-x-2']/ul[last()]//a"))
                  .Click();
            soup = await Soupify();
        }

        chapters.Reverse();
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, chapter) in chapters.Enumerate())
        {
            Print($"Parsing chapter {i + 1} of {chapterCount}");
            CurrentUrl = chapter;
            await LazyLoad(scrollBy: true, increment: 5000);
            soup = await Soupify();
            var post = soup.SelectSingleNode("//p[@class='flex flex-col justify-center items-center']");
            if (post is null)
            {
                await PrintAsync("Post not found", true);
                continue;
            }

            var imgs = post.SelectNodes("./img[@src]").GetSrcs();
            images.AddRange(imgs.Select(img => (StringImageLinkWrapper)img));
        }

        // Files are numbered per chapter, so original will have the files overwrite each other
        return new RipInfo(images, dirName,
            FilenameScheme == FilenameScheme.Original ? FilenameScheme.Chronological : FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for redgifs.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private async Task<RipInfo> RedGifsParse()
    {
        await Task.Delay(3000);
        var baseRequest = "https://api.redgifs.com/v2/gifs?ids=";
        await LazyLoad(scrollBy: true, increment: 1250);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='userName']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var posts = soup.SelectSingleNode("//div[@class='tileFeed']").SelectNodes("./a[@href]").GetHrefs();
        var ids = posts.Select(post => post.Split("/")[^1].Split("#")[0]).ToList();
        var idChunks = ids.Chunk(100);
        var session = new HttpClient();
        var token = await TokenManager.Instance.GetToken("redgifs");
        RequestHeaders["Authorization"] = $"Bearer {token.Value}";
        foreach (var chunk in idChunks)
        {
            var idParam = string.Join("%2C", chunk);
            var request = RequestHeaders.ToRequest(HttpMethod.Get, $"{baseRequest}{idParam}");
            var response = await session.SendAsync(request);
            var responseJson = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
            var gifs = responseJson["gifs"]!.AsArray();
            images.AddRange(gifs
                           .Select(gif => gif!["urls"]!["hd"]!
                               .Deserialize<string>())
                           .Select(gifUrl => gifUrl!)
                           .Select(dummy => (StringImageLinkWrapper)dummy));
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for rule34.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private async Task<RipInfo> Rule34Parse()
    {
        var tags = Rule34Regex().Match(CurrentUrl).Groups[1].Value;
        tags = Uri.UnescapeDataString(tags);
        var dirName = "[Rule34] " + tags.Replace("+", " ").Replace("tags=", "");
        var session = new HttpClient();
        var response =
            await session.GetAsync($"https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&pid=0&{tags}");
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var data = json!.AsArray();
        var images = new List<StringImageLinkWrapper>();
        var pid = 1;
        while (data.Count != 0)
        {
            var urls = data.Select(post => post!["file_url"]!.Deserialize<string>()!);
            images.AddRange(urls.Select(url => (StringImageLinkWrapper)url));
            response = await session.GetAsync(
                $"https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&pid={pid}&{tags}");
            pid += 1;
            json = await response.Content.ReadFromJsonAsync<JsonNode>();
            data = json!.AsArray();
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for sankakucomplex.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private async Task<RipInfo> SankakuComplexParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//header[@class='entry-header']/h1[@class='entry-title']/a").InnerText;
        var imagesBase = soup.SelectNodes("//a[@class='swipebox']").GetHrefs()[1..];
        var images = imagesBase.Select(image => !image.Contains("http") ? $"https:{image}" : image)
                               .Select(dummy => (StringImageLinkWrapper)dummy).ToList();
        return new RipInfo(images, dirName, FilenameScheme);
    }

    private async Task<RipInfo> WnacgParse()
    {
        if (CurrentUrl.Contains("-slist-"))
        {
            CurrentUrl = CurrentUrl.Replace("-slist-", "-index-");
        }
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h2").InnerText;
        //var numImages = soup.SelectSingleNode("//div[@class='asTBcell uwconn']").SelectNodes("./label")[1].InnerText;
        //Console.WriteLine(numImages);
        //numImages = numImages.Split('：')[^1][..^1];
        // Get log_10 of the number of images to get the number of digits
        //var magnitude = (int)Math.Log10(int.Parse(numImages)) + 1;
        var numImages = soup.SelectSingleNode("//span[@class='name tb']").InnerText;
        var magnitude = numImages.Length;
        var imageLinks = new List<string>();

        while (true)
        {
            var imageList = soup
                           .SelectNodes("//li[@class='li tb gallary_item']")
                           .Select(n => n.SelectSingleNode(".//img"))
                           .GetSrcs();
            imageLinks.AddRange(imageList);
            var nextPageButton = soup.SelectSingleNode("//span[@class='next']");
            if (nextPageButton is null)
            {
                break;
            }
            
            var nextPageUrl = nextPageButton.SelectSingleNode(".//a").GetAttributeValue("href", "");
            if (nextPageUrl == "")
            {
                throw new RipperException("Next page url not found");
            }
            
            soup = await Soupify($"https://www.wnacg.com{nextPageUrl}");
        }
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, image) in imageLinks.Enumerate())
        {
            var ext = image.Split(".")[^1];
            var url = $"https:{image}";
            url = url.Replace("t4.", "img5.").Replace("/t/", "/");
            url = "/".Join(url.Split("/")[..^1]);
            url += magnitude switch
            {
                1 => $"/{i + 1:D1}.{ext}",
                2 => $"/{i + 1:D2}.{ext}",
                3 => $"/{i + 1:D3}.{ext}",
                _ => throw new Exception("Invalid number of digits in number of images")
            };
            images.Add(url);
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    #endregion

    private async Task<HtmlNode> Soupify(string? url = null, int delay = 0, LazyLoadArgs? lazyLoadArgs = null,
                                         HttpResponseMessage? response = null, string xpath = "")
    {
        if (response is not null)
        {
            var content = await response.Content.ReadAsStringAsync();
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);
            return htmlDocument.DocumentNode;
        }

        if (url is not null)
        {
            CurrentUrl = url;
        }

        if (delay > 0)
        {
            await Task.Delay(delay);
        }

        if (xpath != "")
        {
            await WaitForElement(xpath);
        }

        if (lazyLoadArgs is not null)
        {
            await LazyLoad(lazyLoadArgs);
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(Driver.PageSource);
        return doc.DocumentNode;
    }

    private async Task<bool> WaitForElement(string xpath, float delay = 0.1f, float timeout = 10)
    {
        var timeoutSpan = TimeSpan.FromSeconds(timeout);
        var startTime = DateTime.Now;
        while (Driver.FindElements(By.XPath(xpath)).Count == 0)
        {
            await Task.Delay((int)(delay * 1000));
            var currTime = DateTime.Now;
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (timeout != -1 && currTime - startTime >= timeoutSpan)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, List<string>> CreateExternalLinkDict()
    {
        var externalLinks = new Dictionary<string, List<string>>();
        foreach (var site in ExternalSites)
        {
            externalLinks[site] = [];
        }

        return externalLinks;
    }

    private static Dictionary<string, List<string>> ExtractExternalUrls(List<string> urls)
    {
        var externalLinks = CreateExternalLinkDict();
        foreach (var site in externalLinks.Keys)
        {
            foreach (var url in urls)
            {
                if (string.IsNullOrEmpty(url) || !url.Contains(site))
                {
                    continue;
                }

                var link = Utility.UrlUtility.ExtractUrl(url);
                if (link != "")
                {
                    externalLinks[site].Add(link + '\n');
                }
            }
        }

        return externalLinks;
    }

    private static Dictionary<string, List<string>> ExtractPossibleExternalUrls(List<string> possibleUrls)
    {
        var externalLinks = CreateExternalLinkDict();
        foreach (var site in externalLinks.Keys)
        {
            foreach (var text in possibleUrls)
            {
                if (!text.Contains(site))
                {
                    continue;
                }

                var parts = text.Split();
                foreach (var part in parts)
                {
                    if (!part.Contains(site))
                    {
                        continue;
                    }

                    var link = Utility.UrlUtility.ExtractUrl(part);
                    if (link != "")
                    {
                        externalLinks[site].Add(link + '\n');
                    }
                }
            }
        }

        return externalLinks;
    }

    private static void SaveExternalLinks(Dictionary<string, List<string>> links)
    {
        foreach (var (site, siteLinks) in links)
        {
            if (siteLinks.Count == 0)
            {
                continue;
            }

            File.AppendAllLines($"{site}_links.txt", siteLinks);
        }
    }

    private static async Task<List<string>> ExtractDownloadableLinks(Dictionary<string, List<string>> srcDict,
                                                                     Dictionary<string, List<string>> dstDict)
    {
        var downloadableLinks = new List<string>();
        var downloadableSites = new[] { "sendvid.com" };
        foreach (var site in srcDict.Keys)
        {
            if (downloadableSites.Contains(site))
            {
                downloadableLinks.AddRange(srcDict[site]);
                srcDict[site].Clear();
            }
            else
            {
                dstDict[site].AddRange(srcDict[site]);
            }
        }

        return await ResolveDownloadableLinks(downloadableLinks);
    }

    private static async Task<List<string>> ResolveDownloadableLinks(List<string> links)
    {
        var resolvedLinks = new List<string>();
        var client = new HttpClient();
        foreach (var link in links)
        {
            if (link.Contains("sendvid.com"))
            {
                var response = await client.GetAsync(link);
                var content = await response.Content.ReadAsStringAsync();
                var soup = new HtmlDocument();
                soup.LoadHtml(content);
                var sourceLink = soup.DocumentNode.SelectSingleNode("//source[@id='video_source']")
                                     .GetAttributeValue("src", "");
                resolvedLinks.Add(sourceLink);
            }
            else
            {
                resolvedLinks.Add(link);
            }
        }

        return resolvedLinks;
    }

    /// <summary>
    ///     Scrolls through the page to lazy load images
    /// </summary>
    /// <param name="args">Arguments for lazy loading</param>
    private static Task LazyLoad(LazyLoadArgs args)
    {
        return LazyLoad(args.ScrollBy, args.Increment, args.ScrollPauseTime, args.ScrollBack, args.ReScroll);
    }
    
    /// <summary>
    ///     Scroll through the page to lazy load images
    /// </summary>
    /// <param name="scrollBy">Whether to scroll through the page or instantly scroll to the bottom</param>
    /// <param name="increment">Distance to scroll by each iteration</param>
    /// <param name="scrollPauseTime">Seconds to wait between each scroll</param>
    /// <param name="scrollBack">Distance to scroll back by after reaching the bottom of the page</param>
    /// <param name="rescroll">Whether scrolling through the page again</param>
    private static async Task LazyLoad(bool scrollBy = false, int increment = 2500, int scrollPauseTime = 500,
                                       int scrollBack = 0, bool rescroll = false)
    {
        var lastHeight = (long)Driver.ExecuteScript("return window.pageYOffset");
        if (rescroll)
        {
            Driver.ExecuteScript("window.scrollTo(0, 0);");
        }

        string scrollScript;
        string heightCheckScript;
        if (scrollBy)
        {
            scrollScript = $"window.scrollBy({{top: {increment}, left: 0, behavior: 'smooth'}});";
            heightCheckScript = "return window.pageYOffset";
        }
        else
        {
            scrollScript = "window.scrollTo(0, document.body.scrollHeight);";
            heightCheckScript = "return document.body.scrollHeight";
        }

        while (true)
        {
            Driver.ExecuteScript(scrollScript);
            await Task.Delay(scrollPauseTime);
            var newHeight = Convert.ToInt64(Driver.ExecuteScript(heightCheckScript));
            if (newHeight == lastHeight)
            {
                if (scrollBack > 0)
                {
                    for (var i = 0; i < scrollBack; i++)
                    {
                        Driver.ExecuteScript($"window.scrollBy({{top: {-increment}, left: 0, behavior: 'smooth'}});");
                        await Task.Delay(scrollPauseTime);
                    }

                    await Task.Delay(scrollPauseTime);
                }

                break;
            }

            lastHeight = newHeight;
        }
        //Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10); // TODO: Check if this is necessary
    }

    private static void Print(object? value)
    {
        Console.WriteLine(value);
    }

    private static async Task PrintAsync(string? value, bool error = false)
    {
        if (error)
        {
            await Console.Error.WriteLineAsync(value);
        }
        else
        {
            await Console.Out.WriteLineAsync(value);
        }
    }

    #region Parser Testing

    public async Task<RipInfo> TestParse(string givenUrl, bool debug, bool printSite)
    {
        try
        {
            var options = InitializeOptions(debug ? "debug" : "");
            Driver = new FirefoxDriver(options);
            CurrentUrl = givenUrl.Replace("members.", "www.");
            SiteName = TestSiteCheck(givenUrl);
            if (SiteName == "999hentai")
            {
                SiteName = "nine99hentai";
            }

            Print($"Testing: {SiteName}Parse");
            var start = DateTime.Now;
            var data = await EvaluateParser(SiteName);
            var end = DateTime.Now;
            Print(data.Urls[0].Referer);
            Print($"Time Elapsed: {end - start}");
            var outData = data.Urls.Select(d => d.Url).ToList();
            JsonUtility.Serialize("test.json", outData);
            if (debug)
            {
                Print("Press any key to exit...");
                Console.ReadKey();
            }

            return data;
        }
        catch
        {
            await File.WriteAllTextAsync("test.html", Driver.PageSource);
            //var screenshot = Driver.GetFullPageScreenshot();
            var screenshot = Driver.GetScreenshot();
            screenshot.SaveAsFile("test.png");
            throw;
        }
        finally
        {
            if (printSite)
            {
                await File.WriteAllTextAsync("test.html", Driver.PageSource);
            }

            Driver.Quit();
        }
    }

    private Task<RipInfo> EvaluateParser(string siteName)
    {
        siteName = siteName[0].ToString().ToUpper() + siteName[1..];
        var methodName = $"{siteName}Parse";
        var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method != null)
        {
            try
            {
                // The second parameter is null because the method (usually) has no parameters
                return (Task<RipInfo>)(method.Invoke(this, null) ?? new InvalidOperationException());
            }
            catch (TargetParameterCountException)
            {
                // The second parameter is string
                return (Task<RipInfo>)(method.Invoke(this, [""]) ?? new InvalidOperationException());
            }
        }

        var methods = typeof(HtmlParser).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
        var normalizedName = siteName.ToLower();
        foreach (var m in methods)
        {
            if (m.Name.Contains(normalizedName, StringComparison.CurrentCultureIgnoreCase))
            {
                return (Task<RipInfo>)(m.Invoke(this, null) ??
                                       throw new InvalidOperationException()); // The second parameter is null because the method has no parameters
            }
        }

        // Handle the case where the method does not exist
        Print($"Method {methodName} not found.");
        throw new InvalidOperationException();
    }

    private string TestSiteCheck(string url)
    {
        var domain = new Uri(url).Host;
        RequestHeaders["referer"] = $"https://{domain}/";
        domain = DomainNameOverride(domain);
        if (url.Contains("https://members.hanime.tv/") || url.Contains("https://hanime.tv/"))
        {
            RequestHeaders["referer"] = "https://cdn.discordapp.com/";
        }
        else if (url.Contains("https://kemono.party/"))
        {
            RequestHeaders["referer"] = "";
        }

        return domain;
    }

    private static string DomainNameOverride(string url)
    {
        string[] specialDomains = ["inven.co.kr", "danbooru.donmai.us"];
        var urlSplit = url.Split(".");
        return specialDomains.Any(url.Contains) ? urlSplit[^3] : urlSplit[^2];
    }

    [GeneratedRegex("(tags=[^&]+)")]
    private static partial Regex Rule34Regex();

    #endregion
}