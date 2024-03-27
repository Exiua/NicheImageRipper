using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Core.Enums;

namespace Core;

public class ImageLink
{
    public string Referer { get; set; }
    public LinkInfo LinkInfo { get; set; }
    public string Url { get; set; }
    public string Filename { get; set; }
    
    public bool IsBlob => Url.StartsWith("blob:");

    public ImageLink(string url, FilenameScheme filenameScheme, int index, string filename = "", LinkInfo linkInfo = LinkInfo.None)
    {
        Referer = "";
        LinkInfo = linkInfo;
        Url = GenerateUrl(url);
        Filename = filename;
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
    
    private string GenerateUrl(string url)
    {
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
        
        return url;
    }

    private string GenerateFilename(string url, FilenameScheme filenameScheme, int index, string filename = "")
    {
        if(filename != "")
        {
            filename = ExtractFilename(url);
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
                var hash5 = MD5.HashData(Encoding.UTF8.GetBytes(url)).ToString();
                return hash5 + ext;
            }
            case FilenameScheme.Chronological:
                return index + ext;
            case FilenameScheme.Original: // Handled above
            default:
                throw new Exception($"FilenameScheme out of bounds: {filenameScheme}"); // TODO: RipperError
        }
    }

    private string ExtractFilename(string url)
    {
        string fileName;
        if(url.Contains("https://titsintops.com/") && url[^1] == '/')
        {
            fileName = url.Split("/")[^2];
            fileName = Regex.Replace(fileName, @"-(jpg|png|webp|mp4|mov|avi|wmv)\.\d+/?", ".$1");
        }
        else if(url.Contains("sendvid.com") && url.Contains(".m3u8"))
        {
            fileName = url.Split("/")[6];
            LinkInfo = LinkInfo.M3U8;
        }
        else if(url.Contains("iframe.mediadelivery.net"))
        {
            fileName = url.Split("/")[^2];
        }
        else if(url.Contains("erocdn.co"))
        {
            var parts = url.Split("/");
            var ext = parts[^1].Split(".")[^1];
            fileName = $"{parts[^2]}.{ext}";
        }
        else if(url.Contains("thothub.lol/") && url.Contains("/?rnd="))
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
            fileName = url.Split("/")[8];
            LinkInfo = LinkInfo.M3U8;
        }
        else
        {
            fileName = Path.GetFileName(new Uri(url).LocalPath);
        }

        return fileName;
    }
    
    public override string ToString()
    {
        return Referer != "" ? $"({Url}, {Filename}, {Referer}, {LinkInfo})" : $"({Url}, {Filename}, {LinkInfo})";
    }
}