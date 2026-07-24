using System.Text.RegularExpressions;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed partial class YoutubeLinkRule : ISiteLinkRule
{
    public string RuleName => "youtube";

    public bool Matches(string url) => url.Contains("youtube.com") || url.Contains("youtu.be");

    public SiteLinkInfo Resolve(string url)
    {
        var normalizedUrl = NormalizeUrl(url);
        var filename = ExtractVideoId(normalizedUrl);
        return new SiteLinkInfo(normalizedUrl, LinkInfo.YoutubeVideo, Filename: filename);
    }

    private static string NormalizeUrl(string url)
    {
        if (url.Contains("youtu.be"))
        {
            var match = YoutuBeRegex().Match(url);
            return match.Success ? $"https://www.youtube.com/watch?v={match.Groups[1].Value}" : url;
        }

        if (url.Contains("/embed/"))
        {
            var match = YoutubeEmbedRegex().Match(url);
            return match.Success ? $"https://www.youtube.com/watch?v={match.Groups[1].Value}" : url;
        }

        // Shorts and standard watch URLs need no normalization
        return url;
    }

    private static string ExtractVideoId(string url)
    {
        return url.Contains("/shorts/")
            ? url.Split("/")[^1].Split("?")[0] // strip any trailing query string
            : url.Split("v=")[1].Split("&")[0];
    }

    [GeneratedRegex(@"youtu\.be/([a-zA-Z0-9-_]+)")]
    private static partial Regex YoutuBeRegex();

    [GeneratedRegex("/embed/([a-zA-Z0-9-_]+)")]
    private static partial Regex YoutubeEmbedRegex();
}