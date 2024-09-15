using System.Text.RegularExpressions;

namespace Core.Utility;

public static partial class ExtractionUtility
{
    public static (List<string> anchorTags, string remaining) ExtractAnchorTagsFromString(string input)
    {
        var anchorTags = new List<string>();
        var matches = AnchorHrefRegex().Matches(input);
        foreach (Match match in matches)
        {
            anchorTags.Add(match.Groups[1].Value);
        }
        
        var remaining = AnchorHrefRegex().Replace(input, "");

        return (anchorTags, remaining);
    }

    [GeneratedRegex("""<a.*?href=\\"(.*?)\\".*?>.*?<\/a>""")]
    private static partial Regex AnchorHrefRegex();
}