using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Core.Exceptions;

namespace Core.Utility;

public static partial class UrlUtility
{
    private static readonly FrozenSet<string> SupportedSites = new[]
    {
        "https://imhentai.xxx/", "https://hotgirl.asia/", "https://www.redpornblog.com/",
        "https://girlsreleased.com/", "https://www.bustybloom.com/",
        "https://www.morazzia.com/",
        "https://www.silkengirl.com/", "https://www.babesandgirls.com/",
        "https://www.babeimpact.com/", "https://www.100bucksbabes.com/",
        "https://www.sexykittenporn.com/", "https://www.babesbang.com/",
        "https://www.exgirlfriendmarket.com/",
        "https://www.novoporn.com/", "https://www.hottystop.com/",
        "https://www.babeuniversum.com/",
        "https://www.babesandbitches.net/", "https://www.chickteases.com/",
        "https://www.wantedbabes.com/", "https://cyberdrop.me/",
        "https://www.sexy-egirls.com/", "https://www.pleasuregirl.net/",
        "https://www.sexyaporno.com/",
        "https://www.theomegaproject.org/", "https://www.babesmachine.com/",
        "https://www.babesinporn.com/",
        "https://www.livejasminbabes.net/", "https://www.grabpussy.com/",
        "https://www.simply-cosplay.com/",
        "https://www.simply-porn.com/", "https://pmatehunter.com/", "https://www.elitebabes.com/",
        "https://www.xarthunter.com/",
        "https://www.joymiihub.com/", "https://www.metarthunter.com/",
        "https://www.femjoyhunter.com/", "https://www.ftvhunter.com/",
        "https://www.hegrehunter.com/", "https://hanime.tv/", "https://members.hanime.tv/",
        "https://www.babesaround.com/", "https://www.8boobs.com/",
        "https://www.decorativemodels.com/", "https://www.girlsofdesire.org/",
        "https://www.foxhq.com/",
        "https://www.rabbitsfun.com/", "https://www.erosberry.com/", "https://www.novohot.com/",
        "https://eahentai.com/",
        "https://www.nightdreambabe.com/", "https://xmissy.nl/", "https://www.glam0ur.com/",
        "https://www.dirtyyoungbitches.com/", "https://www.rossoporn.com/",
        "https://www.nakedgirls.xxx/", "https://www.mainbabes.com/",
        "https://www.hotstunners.com/", "https://www.sexynakeds.com/",
        "https://www.nudity911.com/", "https://www.pbabes.com/",
        "https://www.sexybabesart.com/", "https://hustlebootytemptats.com/", "https://sexhd.pics/",
        "http://www.gyrls.com/",
        "https://www.sensualgirls.org/",
        "https://www.novoglam.com/", "https://www.cherrynudes.com/", "https://www.join2babes.com/",
        "https://gofile.io/", "https://www.babecentrum.com/", "https://www.cutegirlporn.com/",
        "https://everia.club/",
        "https://imgbox.com/", "https://myhentaigallery.com/",
        "https://buondua.com/", "https://f5girls.com/", "https://hentairox.com/",
        "https://www.redgifs.com/", "https://kemono.su/", "https://www.sankakucomplex.com/",
        "https://www.luscious.net/", "https://sxchinesegirlz.one/",
        "https://www.v2ph.com/",
        "https://nudebird.biz/", "https://bestprettygirl.com/", "https://coomer.su/",
        "https://imgur.com/", "https://www.inven.co.kr/",
        "https://arca.live/",
        "https://www.cool18.com/", "https://putmega.com/",
        "https://comics.8muses.com/",
        "https://www.jkforum.net/",
        "https://leakedbb.com/", "https://e-hentai.org/",
        "https://www.artstation.com/",
        "https://porn3dx.com/", "https://www.deviantart.com/", "https://readmanganato.com/",
        "https://manganato.com/",
        "https://sfmcompile.club/", "https://www.tsumino.com/", "https://danbooru.donmai.us/",
        "https://www.flickr.com/", "https://rule34.xxx/", "https://titsintops.com/",
        "https://gelbooru.com/", "https://animeh.to/", "https://fapello.com/",
        "https://nijie.info/", "https://faponic.com/", "https://erothots.co/",
        "https://bitchesgirls.com/", "https://thothub.lol/", "https://influencersgonewild.com/",
        "https://www.erome.com/", "https://ggoorr.net/", "https://drive.google.com/",
        "https://www.dropbox.com/", "https://simpcity.su/", "https://bunkr.si/",
        "https://omegascans.org/", "https://toonily.me/", "https://www.pornhub.com/",
        "https://www.wnacg.com/", "https://sex.micmicdoll.com/", "https://hentai-cosplays.com/",
        "https://x.com/", "https://yande.re/", "https://cup2d.com/", "https://japaneseasmr.com/",
        "https://spacemiss.com/", "https://xiuren.biz/", "https://en.xchina.co/", "https://jpg5.su/",
        "https://simpcity.su/", "https://rule34video.com/", "https://av19a.com/", "https://www.eporner.com/",
        "https://cgcosplay.org/", "https://www.4khd.com/", "https://cosplay69.net/"
    }.ToFrozenSet();

    /// <summary>
    ///     Check the url to make sure it is from valid site
    /// </summary>
    /// <param name="givenUrl">Url to validate</param>
    /// <returns>True if url is for a supported site</returns>
    public static bool UrlCheck(string givenUrl)
    {
        var parsedUri = new Uri(givenUrl);
        var baseUrl = $"{parsedUri.Scheme}://{parsedUri.Host}/";
        return SupportedSites.Contains(baseUrl) || baseUrl.Contains("newgrounds.com");
    }

    /// <summary>
    ///     Check the site and return the domain and delay between requests. Also sets the referer header
    /// </summary>
    /// <param name="givenUrl">Url to check</param>
    /// <param name="requestHeaders">Request headers to modify</param>
    /// <returns>Domain and delay between requests</returns>
    /// <exception cref="RipperException">If the site is not supported</exception>
    public static (string, float) SiteCheck(string givenUrl, Dictionary<string, string> requestHeaders)
    {
        if (!UrlCheck(givenUrl))
        {
            throw new RipperException("Not a support site");
        }

        string[] specialDomains = ["inven.co.kr", "danbooru.donmai.us"];
        var domain = new Uri(givenUrl).Host;
        requestHeaders["referer"] = $"https://{domain}/";
        var domainParts = domain.Split('.');
        domain = specialDomains.Any(specialDomain => domain.Contains(specialDomain))
            ? domainParts[^3]
            : domainParts[^2];
        if (givenUrl.Contains("https://members.hanime.tv/") || givenUrl.Contains("https://hanime.tv/"))
        {
            requestHeaders["referer"] = "https://cdn.discordapp.com/";
        }
        else if (givenUrl.Contains("https://kemono.party/") || givenUrl.Contains("inven.co.kr"))
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
                    PrintUtility.Print($"Incorrect Match: {url}");
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
                    PrintUtility.Print(url);
                    return "";
            }

            return url.Length < end ? "" : url[start..end];
        }

        PrintUtility.Print(url);
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
            PrintUtility.Print(url);
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
                PrintUtility.Print($"Incorrect Match: {url}");
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