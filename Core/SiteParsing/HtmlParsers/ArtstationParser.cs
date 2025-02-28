using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.DataStructures;
using Core.Enums;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ArtstationParser : HtmlParser
{
    public ArtstationParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }
    
    // TODO: Fix this
    /// <summary>
    ///     Parses the html for artstation.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
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
            Log.Information("Page {PageCount} of {Total}", pageCount, total);
            var url = $"https://www.artstation.com/users/{username}/projects.json?page={pageCount}";
            Log.Debug("Requesting {Url}", url);
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
}