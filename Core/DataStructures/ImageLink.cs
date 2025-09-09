using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Core.Enums;
using Core.Exceptions;
using JetBrains.Annotations;
using Core.ExtensionMethods;
using Core.Utility;

namespace Core.DataStructures;

public partial class ImageLink
{
    public string? Referer { get; set; }
    public LinkInfo LinkInfo { get; set; } = LinkInfo.None;
    public string Url { get; set; } = null!;
    public string Filename { get; set; } = null!;
    
    public bool IsBlob => Url.StartsWith("blob:");
    public bool IsInvalid => Url == "";
    
    [MemberNotNullWhen(true, nameof(Referer))]
    public bool HasReferer => !string.IsNullOrEmpty(Referer);
    
    public static ImageLink Invalid => new()
    {
        Referer = null,
        LinkInfo = LinkInfo.None,
        Url = "",
        Filename = "",
    };

    // Only used for (de)serialization
    [UsedImplicitly]
    public ImageLink()
    {
        
    }
    
    public ImageLink(string url, FilenameScheme filenameScheme, int index, string filename = "", 
                     LinkInfo linkInfo = LinkInfo.None, string? referer = "")
    {
        Referer = referer;
        LinkInfo = linkInfo;
        Url = GenerateUrl(url);
        Filename = GenerateFilename(url, filenameScheme, index, filename);
    }

    public void Rename(int index)
    {
        var ext = Path.GetExtension(Filename);
        Filename = index + ext;
    }
    
    public void Rename(string newStem)
    {
        var ext = Path.GetExtension(Filename);
        Filename = newStem + ext;
    }
    
    public void RegenerateFilename(FilenameScheme filenameScheme, int index)
    {
        Filename = GenerateFilename(Url, filenameScheme, index);
    }
    
    public bool Contains(string url)
    {
        return Url.Contains(url);
    }
    
    private string GenerateUrl(string url)
    {
        url = HttpUtility.HtmlDecode(url);
        
        if (url.StartsWith("text:"))
        {
            LinkInfo = LinkInfo.Text;
            return url[5..];
        }
        
        url = url.Replace("\n", "");
        if (url.Contains("iframe.mediadelivery.net"))
        {
            var split = url.Split("}");
            var playlistUrl = split[0];
            Referer = split[1];
            LinkInfo = LinkInfo.IframeMedia;
            var linkUrl = playlistUrl.Split("{")[0];
            return linkUrl;
        }

        if (url.Contains("drive.google.com"))
        {
            LinkInfo = LinkInfo.GDrive;
            return url;
        }
        
        if (url.Contains("mega.nz"))
        {
            LinkInfo = LinkInfo.Mega;
            return url;
        }
        
        if (url.Contains("saint.to"))
        {
            Referer = "https://saint.to/";
            return url;
        }

        if (url.Contains("bunkr.") || url.Contains("bunkr-cache."))
        {
            Referer = "https://get.bunkrr.su/";
            return url;
        }

        if (url.Contains("gofile.io"))
        {
            Referer = "https://gofile.io/";
            return url;
        }
        
        if (url.Contains("youtube.com"))
        {
            LinkInfo = LinkInfo.Youtube;
            if (!url.Contains("/embed/"))
            {
                return url;
            }

            var match = YoutubeEmbedRegex().Match(url);
            return match.Success ? $"https://www.youtube.com/watch?v={match.Groups[1].Value}" : url;
        }
        
        if (url.StartsWith("data:image/") && url.Contains(";base64,"))
        {
            LinkInfo = LinkInfo.Base64;
            return url;
        }
        
        return url.StartsWith("//") ? $"https:{url}" : url;
    }

    private string GenerateFilename(string url, FilenameScheme filenameScheme, int index, string filename = "")
    {
        if(filename == "")
        {
            switch (LinkInfo)
            {
                case LinkInfo.Text:
                    return "urls.txt";
                case LinkInfo.Base64:
                {
                    var b64Ext = url.Split("image/")[1].Split(";")[0];
                    var hash = BytesToString(MD5.HashData(Encoding.UTF8.GetBytes(url)));
                    filename = $"{hash}.{b64Ext}";
                    break;
                }
                case LinkInfo.None:
                case LinkInfo.M3U8Ffmpeg:
                case LinkInfo.GDrive:
                case LinkInfo.IframeMedia:
                case LinkInfo.Mega:
                case LinkInfo.PixelDrain:
                case LinkInfo.Youtube:
                case LinkInfo.GoFile:
                case LinkInfo.MpegDash:
                case LinkInfo.ResolveImage:
                case LinkInfo.M3U8YtDlp:
                case LinkInfo.SeleniumImage:
                case LinkInfo.ObfuscatedM3U8:
                default:
                    filename = ExtractFilename(url);
                    break;
            }
        }
        
        if(filenameScheme == FilenameScheme.Original)
        {
            if(filename.Contains('%'))
            {
                filename = Uri.UnescapeDataString(filename);
            }
            return filename;
        }

        var ext = Path.GetExtension(filename);
        switch (filenameScheme)
        {
            case FilenameScheme.Hash:
            {
                var hash5 = BytesToString(MD5.HashData(Encoding.UTF8.GetBytes(url)));
                return hash5 + ext;
            }
            case FilenameScheme.Chronological:
                return index + ext;
            case FilenameScheme.Original: // Handled above
            default:
                throw new RipperException($"FilenameScheme out of bounds: {filenameScheme}");
        }
    }

    private string ExtractFilename(string url)
    {
        string fileName;
        if(url.Contains("https://titsintops.com/") && url[^1] == '/')
        {
            fileName = url.Split("/")[^2];
            fileName = ExtensionRegex().Replace(fileName, ".$1");
        }
        else if(url.Contains("sendvid.com") && url.Contains(".m3u8"))
        {
            fileName = url.Split("/")[6];
            LinkInfo = LinkInfo.M3U8Ffmpeg;
        }
        else if(url.Contains("iframe.mediadelivery.net"))
        {
            fileName = url.Split("/")[^1].Split("?")[0] + ".mp4"; // Assume mp4
        }
        else if(url.Contains("erocdn.co"))
        {
            var parts = url.Split("/");
            var ext = parts[^1].Split(".")[^1];
            fileName = $"{parts[^2]}.{ext}";
        }
        else if(url.Contains("thothub.lol/") && (url.Contains("/?rnd=") || url.Contains("get_image") || url.Contains("get_file")))
        {
            fileName = url.Split("/")[^2];
        }
        else if((url.Contains("kemono.su/") || url.Contains("coomer.su/")) && url.Contains("?f="))
        {
            fileName = url.Split("?f=")[^1];
            if(fileName.Contains("http"))
            {
                fileName = url.Split("?f=")[0].Split("/")[^1];
            }
        }
        else if(url.Contains("phncdn.com"))
        {
            var parts = url.Split("/");
            fileName = parts.Length >= 9 ? parts[8] : parts[^1].Split(")")[0];
            LinkInfo = url.Contains(".m3u8") ? LinkInfo.M3U8Ffmpeg : LinkInfo.None;
        }
        else if (url.Contains("yande.re/"))
        {
            fileName = url.Split("/")[^1];
            fileName = fileName.Remove("yande.re").Replace("%20", "-");
        }
        else if(url.Contains("playhls.com/"))
        {
            fileName = url.Split("&id=")[^1];
            fileName = fileName.Split("&")[0] + ".mp4";
        }
        else if (url.Contains("rule34video.com"))
        {
            fileName = url.Split("download_filename=")[1];
            fileName = fileName.Split("&")[0];
        }
        else if (url.Contains("//vkvd") && url.Contains("okcdn.ru"))
        {
            if (url.Contains("id="))
            {
                fileName = url.Split("id=")[1].Split("&")[0] + ".mp4";
            }
            else
            {
                fileName = url.Split(".")[^2] + ".mp4";
            }
            
            LinkInfo = LinkInfo.MpegDash;
        }
        else if (url.Contains("nlegs.com") || url.Contains("ladylap.com"))
        {
            fileName = url.Split("url=")[1].Split("&")[0] + ".jfif";
            LinkInfo = LinkInfo.ResolveImage;
        }
        else if (url.Contains("xasiat.com"))
        {
            fileName = url.Split("/")[^2];
        }
        else if (url.Contains("milocdn.com") && url.Contains("master.m3u8") || url.Contains("cdn-centaurus.com") && url.Contains("master.m3u8"))
        {
            fileName = url.Split("t=")[1].Split("&")[0] + ".mp4";
            LinkInfo = LinkInfo.M3U8YtDlp;
        }
        else if (url.Contains("xx-media.knit.bid"))
        {
            fileName = url.Split("/")[^1];
            LinkInfo = LinkInfo.SeleniumImage;
        }
        else if (url.Contains("69tang.org"))
        {
            fileName = url.Split("/")[^2];
        }
        else if (url.Contains("ddyunbo.com"))
        {
            fileName = url.Split("/")[^2] + ".mp4";
            LinkInfo = LinkInfo.M3U8Ffmpeg;
        }
        else if (url.Contains("fcww0.com"))
        {
            fileName = url.Split("/")[^2];
        }
        else if (url.Contains("shameless.com"))
        {
            fileName = url.Split("/")[8];
        }
        else if (url.Contains("porndr.com"))
        {
            fileName = url.Split("/")[^2];
        }
        else if (url.Contains("abxxx.com"))
        {
            fileName = url.Split("/")[^2];
        }
        else if (url.Contains("love4porn.com"))
        {
            fileName = url.Split("/")[^2];
        }
        else if(url.Contains("asianviralhub.com"))
        {
            fileName = url.Split("/")[^2];
        }
        else if(url.Contains("hdzog.com"))
        {
            fileName = url.Split("/")[^2];
        }
        else if(url.Contains("privatehomeclips.com"))
        {
            fileName = url.Split("/")[^2];
        }
        else if(url.Contains("x-x-x.tube"))
        {
            fileName = url.Split("/")[^2];
        }
        else
        {
            fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (url.Contains(".m3u8"))
            {
                LinkInfo = LinkInfo.M3U8Ffmpeg;
                fileName = fileName.Replace(".m3u8", ".mp4");
            }
        }

        return FilesystemUtility.CleanPathStem(fileName);
    }
    
    public override string ToString()
    {
        var linkInfo = Enum.GetName(LinkInfo);
        var url = LinkInfo == LinkInfo.Base64 ? UrlUtility.TruncateLongUrl(Url) : Url;
        return Referer != "" ? $"({url}, {Filename}, {Referer}, {linkInfo})" : $"({url}, {Filename}, {linkInfo})";
    }
    
    private static string BytesToString(byte[] bytes)
    {
        var sb = new StringBuilder(32);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("X2"));
        }
        return sb.ToString();
    }

    [GeneratedRegex(@"-(jpg|png|webp|mp4|mov|avi|wmv)\.\d+/?")]
    private static partial Regex ExtensionRegex();
    [GeneratedRegex("/embed/([a-zA-Z0-9-_]+)")]
    private static partial Regex YoutubeEmbedRegex();
}