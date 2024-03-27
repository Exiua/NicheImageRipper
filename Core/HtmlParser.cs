using System.Reflection;
using Core.Enums;
using OpenQA.Selenium.Firefox;

namespace Core;

public class HtmlParser
{
    private const string DriverHeader =
        "user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0";

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

    public RipInfo ParseSite(string url)
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
            var siteInfo = siteParser();
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
    
    private Func<RipInfo> GetParser(string siteName)
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

    private RipInfo DotPartyParse(string domainUrl)
    {
        throw new NotImplementedException();
    }
    
    private RipInfo ImhentaiParse()
    {
        throw new NotImplementedException();
    }
    
    private RipInfo KemonoParse()
    {
        return DotPartyParse("https://kemono.party");
    }

    #endregion
    
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
    
    /*
     * def _test_site_check(self, url: str) -> str:
        domain = urlparse(url).netloc
        requests_header['referer'] = "".join([SCHEME, domain, "/"])
        domain = self._domain_name_override(domain)
        # Hosts images on a different domain
        if "https://members.hanime.tv/" in url or "https://hanime.tv/" in url:
            requests_header['referer'] = "https://cdn.discordapp.com/"
        elif "https://kemono.party/" in url:
            requests_header['referer'] = ""
        return domain
     */

    private static string DomainNameOverride(string url)
    {
        string[] specialDomains = ["inven.co.kr", "danbooru.donmai.us"];
        var urlSplit = url.Split(".");
        return specialDomains.Any(url.Contains) ? urlSplit[^3] : urlSplit[^2];
    }
}