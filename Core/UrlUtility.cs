using System.Text.RegularExpressions;

namespace Core;

public static partial class UrlUtility
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

    public static string ExtractUrl(string url)
    {
        url = url.Replace("</a>", "");
        if (url.Contains("drive.google.com"))
        {
            return GDriveLinkParse(url);
        }

        if (url.Contains("mega.nz"))
        {
            return MegaLinkParse(url);
        }

        var start = url.IndexOf("https:", StringComparison.Ordinal);
        return start != -1 ? url[start..] : url;
    }
    
    private static string GDriveLinkParse(string url)
    {
        var start = url.IndexOf("https:", StringComparison.Ordinal);
        if (start == -1)
        {
            return "";
        }
        
        var m = GDriveLinkRegex1().Match(url);
        if (m.Success)
        {
            int end;
            switch (m.Groups[1].Value)
            {
                case "?usp=sharing":
                    end = m.Groups[1].Index + "?usp=sharing".Length;
                    break;
                case "?usp=share_link":
                    end = m.Groups[1].Index + "?usp=share_link".Length;
                    break;
                case "?id=":
                    end = m.Groups[1].Index + "?id=".Length + 33;
                    break;
                default:
                    Console.WriteLine($"Incorrect Match: {url}");
                    return "";
            }

            return url.Length < end ? "" : url[start..end];
        }
        
        m = GDriveLinkRegex2().Match(url);
        if (m.Success)
        {
            int end;
            switch (m.Groups[1].Value)
            {
                case "/folders/":
                    end = m.Groups[1].Index + "/folders/".Length + 33;
                    break;
                case "/file/d/":
                    end = m.Groups[1].Index + "/file/d/".Length + 33;
                    break;
                default:
                    Console.WriteLine(url);
                    return "";
            }
            
            return url.Length < end ? "" : url[start..end];
        }

        Console.WriteLine(url);
        return "";
    }

    private static string MegaLinkParse(string url)
    {
        var start = url.IndexOf("https:", StringComparison.Ordinal);
        if (start == -1)
        {
            return "";
        }
        var m = MegaLinkRegex().Match(url);
        if (!m.Success)
        {
            Console.WriteLine(url);
            return "";
        }

        int end;
        switch (m.Groups[1].Value)
        {
            case "/folder/":
                end = m.Groups[1].Index + "/folder/".Length + 31;
                break;
            case "/#F!":
                end = m.Groups[1].Index + "/#F!".Length + 31;
                break;
            case "/#!":
                end = m.Groups[1].Index + "/#!".Length + 52;
                break;
            case "/file/":
                end = m.Groups[1].Index + "/file/".Length + 52;
                break;
            default:
                Console.WriteLine($"Incorrect Match: {url}");
                return "";
        }
        
        return url.Length < end ? "" : url[start..end];
    }

    [GeneratedRegex(@"(\?usp=sharing|\?usp=share_link|\?id=)")]
    private static partial Regex GDriveLinkRegex1();
    [GeneratedRegex(@"(/folders/|/file/d/)")]
    private static partial Regex GDriveLinkRegex2();
    [GeneratedRegex(@"(/folder/|/#F!|/#!|/file/)")]
    private static partial Regex MegaLinkRegex();
}