namespace NHentaiApi.Models;

public enum GalleryFormat
{
    Torrent,
    Cbz,
    Zip,
}

public static class GalleryFormatExtensions
{
    public static string ToGalleryFormat(this GalleryFormat format)
    {
        return format switch
        {
            GalleryFormat.Torrent => "torrent",
            GalleryFormat.Cbz => "cbz",
            GalleryFormat.Zip => "zip",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }
}