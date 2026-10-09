namespace PixivApi.Utilities;

public static class PixivExtensionMethods
{
    public static string ToOriginalUrl(this string url)
    {
        var parts = url.Split("/");
        var key = parts.Skip(7).Take(7).ToArray();
        var filename = key[6];
        var ext = filename.Split(".")[1];
        var filenameParts = filename.Split("_");
        var filenameKey = filenameParts[0];
        var filenamePage = filenameParts[1];
        var originalFilename = $"{filenameKey}_{filenamePage}.{ext}";
        key[6] = originalFilename;
        var keyUrl = string.Join("/", key);
        
        return $"https://i.pximg.net/img-original/img/{keyUrl}";
    }
}