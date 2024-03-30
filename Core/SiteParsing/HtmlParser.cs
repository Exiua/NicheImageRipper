using System.Reflection;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;

namespace Core.SiteParsing;

public class HtmlParser
{
    private const string DriverHeader =
        "user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0";

    private static readonly string[] ExternalSites = ["drive.google.com", "mega.nz", "mediafire.com", "sendvid.com", "dropbox.com"];
    private static readonly string[] ParsableSites = ["drive.google.com", "mega.nz", "sendvid.com", "dropbox.com"];
    
    private static bool LoggedIn;
    
    public FirefoxDriver Driver { get; set; }
    public bool Interrupted { get; set; }
    public string SiteName { get; set; }
    public float SleepTime { get; set; }
    public float Jitter { get; set; }
    public string GivenUrl { get; set; }
    public FilenameScheme FilenameScheme { get; set; }
    public Dictionary<string, string> RequestHeaders { get; set; }
    
    public string CurrentUrl
    {
        get => Driver.Url;
        set => Driver.Url = value;
    }

    public HtmlParser(Dictionary<string, string> requestHeaders, string siteName = "",
        FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        var options = InitializeOptions(siteName);
        Driver = new FirefoxDriver(options);
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
        (SiteName, SleepTime) = Utility.UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
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
            Console.WriteLine(CurrentUrl);
            throw;
        }
        finally
        {
            Driver.Quit();
        }
    }
    
    private Func<Task<RipInfo>> GetParser(string siteName)
    {
        return siteName switch
        {
            "imhentai" => ImhentaiParse,
            "kemono" => KemonoParse,
            _ => throw new Exception("Site not supported")
        };
    }
    
    private static Dictionary<string, PartialSaveEntry> ReadPartialSave()
    {
        return JsonUtility.Deserialize<Dictionary<string, PartialSaveEntry>>("partial.json");
    }
    
    private void WritePartialSave(RipInfo ripInfo, string url)
    {
        var partialSaveEntry = new PartialSaveEntry
        {
            Cookies = RequestHeaders["cookie"],
            Referer = RequestHeaders["referer"],
            RipInfo = ripInfo
        };
        var partialSave = new Dictionary<string, PartialSaveEntry> {{url, partialSaveEntry}};
        JsonUtility.Serialize("partial.json", partialSave);
    }

    private static FirefoxOptions InitializeOptions(string siteName)
    {
        var options = new FirefoxOptions();
        if(siteName != "v2ph" || LoggedIn)
        {
            options.AddArgument("-headless");
        }
        options.AddArgument(DriverHeader);
        options.SetPreference("dom.disable_beforeunload", true);
        options.SetPreference("browser.tabs.warnOnClose", false);
        return options;
    }

    #region Site Parsers

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
            Console.WriteLine($"Parsing page {page}");
            var imageList = soup.SelectSingleNode("//div[@class='card-list__items']").SelectNodes("//article");
            foreach (var image in imageList)
            {
                var id = image.GetAttributeValue("data-id", "");
                imageLinks.Add($"{baseUrl}/post/{id}");
            }
            var nextUrl = $"{pageUrl}?o={page * 50}";
            Console.WriteLine(nextUrl);
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
            Console.WriteLine($"Parsing post {i + 1} of {numPosts}");
            Console.WriteLine(link);
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
            if (imageListContainer is not null)
            {
                var imageList = imageListContainer.SelectNodes("//a[@class='fileThumb image-link']");
                var imageListLinks = imageList.GetHrefs();
                images.AddRange(imageListLinks);
            }
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
        
        return new RipInfo(stringLinks, dirName, FilenameScheme);
    }

    private async Task<RipInfo> DropboxParse(string dropboxUrl = "")
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
                var deletedNotice = soup.SelectSingleNode("//h2[@class='dig-Title dig-Title--size-large dig-Title--color-standard']");
                if (deletedNotice is not null)
                {
                    return new RipInfo([], "Deleted", FilenameScheme);
                }
                Console.WriteLine(CurrentUrl);
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
                GetDropboxFile(soup, post, filenames, images, posts); // The method modifies the posts list which is being iterated over...
            }
        }
        else
        {
            GetDropboxFile(soup, CurrentUrl, filenames, images, posts);
        }
        return new RipInfo(images.ToWrapperList(), dirName, FilenameScheme, filenames: filenames);
    }
    
    private static void GetDropboxFile(HtmlNode soup, string post, List<string> filenames, List<string> images, List<string> posts)
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
    
    private async Task<RipInfo> KemonoParse()
    {
        return await DotPartyParse("https://kemono.party");
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
        if(images == "")
        {
            throw new NotFoundException("Image not found");
        }
        var numPages = int.Parse(soup.SelectSingleNode("//li[@class='pages']").InnerText.Split()[1]);
        var dirName = soup.SelectSingleNode("//h1").InnerText;
        
        return new RipInfo([images], dirName, generate: true, numUrls: numPages);
    }

    #endregion

    private async Task<HtmlNode> Soupify(string? url = null, int delay = 0, HttpResponseMessage? response = null, string xpath = "")
    {
        if(response is not null)
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
            await Task.Delay((int) (delay * 1000));
            var currTime = DateTime.Now;
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (timeout != -1 && currTime - startTime >= timeoutSpan)
            {
                return false;
            }
        }
        return true;
    }
    
    /*
     * def __wait_for_element(self, xpath: str, delay: float = 0.1, timeout: float = 10) -> bool:
        if timeout != -1:
            timeout *= 1_000_000_000  # Convert seconds to nanoseconds
        start_time = time.time_ns()
        while not self.driver.find_elements(By.XPATH, xpath):
            sleep(delay)
            curr_time = time.time_ns()
            if timeout != -1 and curr_time - start_time >= timeout:
                return False
        return True
     */
    
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
    
    private static async Task<List<string>> ExtractDownloadableLinks(Dictionary<string, List<string>> srcDict, Dictionary<string, List<string>> dstDict)
    {
        var downloadableLinks = new List<string>();
        var downloadableSites = new[] {"sendvid.com"};
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
                var sourceLink = soup.DocumentNode.SelectSingleNode("//source[@id='video_source']").GetAttributeValue("src", "");
                resolvedLinks.Add(sourceLink);
            }
            else
            {
                resolvedLinks.Add(link);
            }
        }
        return resolvedLinks;
    }
    
    public async Task<RipInfo> TestParse(string givenUrl, bool debug, bool printSite)
    {
        try
        {
            var options = new FirefoxOptions();
            if (!debug)
            {
                options.AddArgument("-headless");
            }
            options.AddArgument(DriverHeader);
            Driver = new FirefoxDriver(options);
            CurrentUrl = givenUrl.Replace("members.", "www.");
            SiteName = TestSiteCheck(givenUrl);
            if (SiteName == "999hentai")
            {
                SiteName = "nine99hentai";
            }
            Console.WriteLine($"Testing: {SiteName}Parse");
            var start = DateTime.Now;
            var data = await EvaluateParser(SiteName);
            var end = DateTime.Now;
            Console.WriteLine(data.Urls[0].Referer);
            Console.WriteLine($"Time Elapsed: {end - start}");
            var outData = data.Urls.Select(d => d.Url).ToList();
            JsonUtility.Serialize("test.json", outData);
            return data;
        }
        catch
        {
            await File.WriteAllTextAsync("test.html", Driver.PageSource);
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
        var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method != null)
        {
            return (Task<RipInfo>)(method.Invoke(this, null) ?? throw new InvalidOperationException()); // The second parameter is null because the method has no parameters
        }

        // Handle the case where the method does not exist
        Console.WriteLine($"Method {methodName} not found.");
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
}