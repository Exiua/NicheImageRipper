namespace Core;

public static class UrlUtility
{
    public static (string, float) SiteCheck(string givenUrl, Dictionary<string, string> requestHeaders)
    {
        if(!Utility.UrlCheck(givenUrl))
        {
            throw new Exception("Not a support site"); // TODO: RipperError
        }
        
        var specialDomains = new[] {"inven.co.kr", "danbooru.donmai.us"};
        var domain = new Uri(givenUrl).Host;
        requestHeaders["referer"] = $"https://{domain}/";
        var domainParts = domain.Split('.');
        domain = specialDomains.Any(specialDomain => domain.Contains(specialDomain)) ? domainParts[^3] : domainParts[^2];
        if (givenUrl.Contains("https://members.hanime.tv/") || givenUrl.Contains("https://hanime.tv/"))
        {
            requestHeaders["referer"] = "https://cdn.discordapp.com/";
        }
        else if (givenUrl.Contains("https://kemono.party/"))
        {
            requestHeaders["referer"] = "";
        }
        else if (givenUrl.Contains("https://e-hentai.org/"))
        {
            return (domain, 5f);
        }
        return (domain, 0.2f);
    }
}