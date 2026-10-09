using System.Text.RegularExpressions;

namespace NicheImageRipper.SiteModules.Modules.Booru;

internal static partial class BooruUrlNormalization
{
    internal static string NormalizeUrl(string url, Booru booru)
    {
        var baseUrl = url.Split("?")[0];
        var tags = BooruRegex().Match(url).Groups[1].Value.Replace("++", "+");
        if (tags.EndsWith('+'))
        {
            tags = tags[..^1];
        }

        return booru switch
        {
            Booru.Danbooru or Booru.Yandere or Booru.E621 => $"{baseUrl}?{tags}",
            Booru.Gelbooru or Booru.Rule34 => $"{baseUrl}?page=post&s=list&{tags}",
            _ => throw new ArgumentOutOfRangeException(nameof(booru), booru, null)
        };
    }
    
    [GeneratedRegex(@"(tags=[^&]+)")]
    public static partial Regex BooruRegex();
}