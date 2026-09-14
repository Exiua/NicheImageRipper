using System.Text.RegularExpressions;

namespace NicheImageRipper.SiteModules.Modules.Booru;

internal static partial class BooruUrlNormalization
{
    internal static string NormalizeUrl(string url, Sdk.Enums.Booru booru)
    {
        var baseUrl = url.Split("?")[0];
        var tags = BooruRegex().Match(url).Groups[1].Value.Replace("++", "+");
        if (tags.EndsWith('+'))
        {
            tags = tags[..^1];
        }

        return booru switch
        {
            Sdk.Enums.Booru.Danbooru or Sdk.Enums.Booru.Yandere or Sdk.Enums.Booru.E621 => $"{baseUrl}?{tags}",
            Sdk.Enums.Booru.Gelbooru or Sdk.Enums.Booru.Rule34 => $"{baseUrl}?page=post&s=list&{tags}",
            _ => throw new ArgumentOutOfRangeException(nameof(booru), booru, null)
        };
    }
    
    [GeneratedRegex(@"(tags=[^&]+)")]
    public static partial Regex BooruRegex();
}