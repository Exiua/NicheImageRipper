using System.Reflection;
using Core.Enums;
using HtmlAgilityPack;
using OpenQA.Selenium.Firefox;

namespace Core;

public class HtmlParser
{
    private const string DriverHeader =
        "user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0";

    private static readonly string[] ExternalSites = ["drive.google.com", "mega.nz", "mediafire.com", "sendvid.com", "dropbox.com"];
    
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
        var soup = Soupify();
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
            soup = Soupify(nextUrl);
            if (soup.SelectSingleNode("//h2[@class='site-section__subheading']") is not null || CurrentUrl == origUrl)
            {
                break;
            }
        }

        #endregion

        #region Parse All Posts
        
        var images = new List<ImageLink>();
        throw new NotImplementedException();
        /*
         *  images = []
        external_links: dict[str, list[str]] = self.__create_external_link_dict()
        num_posts = len(image_links)
        ATTACHMENTS = (".zip", ".rar", ".mp4", ".webm", ".psd", ".clip", ".m4v", ".7z", ".jpg", ".png", ".webp")
        for i, link in enumerate(image_links):
            print("".join(["Parsing post ", str(i + 1), " of ", str(num_posts)]))
            print(link)
            soup = self.soupify(link)
            links = soup.find_all("a")
            links = [link.get("href") for link in links]
            possible_links_p = soup.find_all("p")
            possible_links = [tag.text for tag in possible_links_p]
            possible_links_div = soup.find_all("div")
            possible_links.extend([tag.text for tag in possible_links_div])
            ext_links = self.__extract_external_urls(links)
            for site in ext_links:
                external_links[site].extend(ext_links[site])
            ext_links = self.__extract_possible_external_urls(possible_links)
            for site in ext_links:
                external_links[site].extend(ext_links[site])
            attachments = [domain_url + link if domain_url not in link and not any(protocol in link for protocol in
                                                                                   ("https", "http")) else link for link
                           in links if any(ext in link for ext in ATTACHMENTS)]
            # domain + link if domain_url is not in link and link doesn't contain http or https else link
            # for links that contain any of the attachment extensions anywhere in the link
            images.extend(attachments)
            for site in PARSEABLE_SITES:
                images.extend(external_links[site])
            image_list = soup.find("div", class_="post__files")
            if image_list is not None:
                image_list = image_list.find_all("a", class_="fileThumb image-link")
                image_list = [img.get("href") for img in image_list]
                images.extend(image_list)
         */

        #endregion
    }
    
    private async Task<RipInfo> ImhentaiParse()
    {
        throw new NotImplementedException();
    }
    
    private async Task<RipInfo> KemonoParse()
    {
        return await DotPartyParse("https://kemono.party");
    }

    #endregion

    private HtmlNode Soupify(string? url = null)
    {
        if (url is not null)
        {
            CurrentUrl = url;
        }
        var doc = new HtmlDocument();
        doc.LoadHtml(Driver.PageSource);
        return doc.DocumentNode;
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

    private Dictionary<string, List<string>> ExtractExternalUrls(List<string> urls)
    {
        var externalLinks = CreateExternalLinkDict();
        foreach (var site in externalLinks.Keys)
        {
            var extLinks = new List<string>();
            foreach (var url in urls)
            {
                if (string.IsNullOrEmpty(url) || !url.Contains(site))
                {
                    continue;
                }
                var link = UrlUtility.ExtractUrl(url);
                if (link is not null)
                {
                    extLinks.Add(link);
                }
            }
            externalLinks[site].AddRange(extLinks);
        }
        return externalLinks;
    }
    
    /*
     * def __extract_external_urls(self, urls: list[str]) -> dict[str, list[str]]:
        external_links: dict[str, list[str]] = self.__create_external_link_dict()
        for site in external_links.keys():
            # links = [link + "\n" for url in urls if url and site in url and (link := extract_url(url))]
            # external_links[site].extend(links)
            ext_links = []
            for url in urls:
                if not url or site not in url:
                    continue
                link = extract_url(url)
                if link:
                    ext_links.append(link + "\n")
            external_links[site].extend(ext_links)
        return external_links

    def __extract_possible_external_urls(self, possible_urls: list[str]) -> dict[str, list[str]]:
        external_links: dict[str, list[str]] = self.__create_external_link_dict()
        for site in external_links.keys():
            for text in possible_urls:
                if site not in text:
                    continue
                parts = text.split()
                for part in parts:
                    if site not in part:
                        continue
                    link = extract_url(part)
                    if link:
                        external_links[site].append(link + "\n")
        return external_links

    @staticmethod
    def __save_external_links(links: dict[str, list[str]]):
        for site in links:
            if not links[site]:
                continue
            with open(f"{site}_links.txt", "a", encoding="utf-16") as f:
                f.writelines(links[site])

    def __extract_downloadable_links(self, src_dict: dict[str, list[str]], dst_dict: dict[str, list[str]]) -> list[str]:
        """
            Extract links that can be downloaded while copying links that cannot be downloaded to dst_dict.
            Returns list of links that can be downloaded.
        """
        downloadable_links = []
        downloadable_sites = ("sendvid.com",)
        for site in src_dict:
            if any(s == site for s in downloadable_sites):
                downloadable_links.extend(src_dict[site])
                src_dict[site].clear()
            else:
                dst_dict[site].extend(src_dict[site])
        return self.__resolve_downloadable_links(downloadable_links)

     */
    
    public RipInfo TestParse(string givenUrl, bool debug, bool printSite)
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
            var data = EvaluateParser(SiteName);
            var end = DateTime.Now;
            Console.WriteLine(data.Urls[0].Referer);
            Console.WriteLine($"Time Elapsed: {end - start}");
            var outData = data.Urls.Select(d => d.Url).ToList();
            JsonUtility.Serialize("test.json", outData);
            return data;
        }
        catch
        {
            File.WriteAllText("test.html", Driver.PageSource);
            throw;
        }
        finally
        {
            if (printSite)
            {
                File.WriteAllText("test.html", Driver.PageSource);
            }
            Driver.Quit();
        }
    }

    private RipInfo EvaluateParser(string siteName)
    {
        var methodName = $"{siteName}Parse";
        var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method != null)
        {
            return (RipInfo)(method.Invoke(this, null) ?? throw new InvalidOperationException()); // The second parameter is null because the method has no parameters
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