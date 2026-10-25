using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.SiteParsing;
using Serilog;

namespace NicheImageRipper.SiteModules.Modules.Booru;

/// <summary>
///     Expands <c>booru &lt;tags or booru search URLs&gt;</c> into searches across every supported booru.
/// </summary>
/// <remarks>
///     Plain words are tags for one search. A booru search URL (anything with <c>tags=</c>) ends the current search and
///     becomes its own one, so <c>booru cute tall https://example.booru.com/post?tags=cat+animal funny</c> queues three
///     searches: (cute, tall), (cat, animal) and (funny).
/// </remarks>
public sealed class BooruInputExpander : IQueueInputExpander
{
    private const string Keyword = "booru";
    private const string SearchUrl = "https://booru.com/post?";

    private static readonly ILogger Logger = Log.ForContext<BooruInputExpander>();

    public IReadOnlyList<string>? TryExpand(string input)
    {
        var parts = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].Equals(Keyword, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (parts.Length == 1)
        {
            Logger.Warning("Missing argument: booru <tags>");
            return [];
        }

        var urls = new List<string>();
        var tags = new List<string>();
        foreach (var part in parts.Skip(1))
        {
            if (!part.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add(part);
                continue;
            }

            FlushTags();
            try
            {
                var tagsQuery = BooruParser.ExtractTagsFromUrl(part).TrimEnd('+');
                urls.Add(SearchUrl + tagsQuery);
            }
            catch (RipperException)
            {
                Logger.Warning("Not a booru search URL (no tags=), skipping: {Url}", part);
            }
        }

        FlushTags();
        return urls;

        void FlushTags()
        {
            if (tags.Count == 0)
            {
                return;
            }

            urls.Add(SearchUrl + "tags=" + string.Join("+", tags));
            tags.Clear();
        }
    }
}
