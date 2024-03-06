using System.Net;
using System.Text.Json;
using Core.Enums;

namespace Core;

public class ImageRipper
{
    public Dictionary<string, string> RequestHeaders { get; set; } = new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36",
        ["referer"] = "https://imhentai.xxx/",
        ["cookie"] = "",
        ["Authorization"] = ""
    };
    
    public FilenameScheme FilenameScheme { get; set; }
    public UnzipProtocol UnzipProtocol { get; set; }
    //public RipInfo FolderInfo { get; set; }
    public string GivenUrl { get; set; }
    public bool Interrupted { get; set; }
    public bool AutoExtract { get; set; }
    public Dictionary<string, Dictionary<string, string>> Logins { get; set; }
    public bool LoggedIn { get; set; }
    public Dictionary<string, bool> PersistentLogins { get; set; }
    public string SavePath { get; set; }
    public HttpClient Session { get; set; }
    public string SiteName { get; set; }
    public float SleepTime { get; set; }
    public int CurrentIndex { get; set; }

    public ImageRipper(FilenameScheme filenameScheme = FilenameScheme.Original, UnzipProtocol unzipProtocol = UnzipProtocol.None)
    {
        FilenameScheme = filenameScheme;
        UnzipProtocol = unzipProtocol;
        //FolderInfo = null;
        GivenUrl = "";
        Interrupted = false;
        AutoExtract = false;
        //Logins = Config.Config.Logins;
        LoggedIn = File.Exists("cookies.pkl");
        PersistentLogins = new Dictionary<string, bool>();
        //SavePath = Config.Config["SavePath"];
        Session = new HttpClient();
        SiteName = "";
        SleepTime = 0.2f;
        CurrentIndex = 0;
    }

    public void Rip(string url)
    {
        SleepTime = 0.2f;   // Reset sleep time
        GivenUrl = url.Replace("members.", "www."); // Replace is done to properly parse hanime pages
        (SiteName, SleepTime) = UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        if (CookiesNeeded())
        {
            AddCookies();
        }

        FileGetter();
    }

    private void FileGetter()
    {
        var htmlParser = new HtmlParser(RequestHeaders, SiteName, FilenameScheme);
        var folderInfo = htmlParser.ParseSite(GivenUrl);
    }
    
    private void AddCookies()
    {
        throw new NotImplementedException();
    }
    
    private bool CookiesNeeded()
    {
        return SiteName == "titsintops";
    }
}