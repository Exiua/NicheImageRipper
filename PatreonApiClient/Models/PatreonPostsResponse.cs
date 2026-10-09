using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PatreonPostsResponse
{
    [JsonPropertyName("data")]
    public List<PostResource> Data { get; set; } = [];

    [JsonPropertyName("included")]
    public List<IncludedResource> Included { get; set; } = [];

    [JsonPropertyName("links")]
    public PaginationLinks? Links { get; set; }

    [JsonPropertyName("meta")]
    public ResponseMeta? Meta { get; set; }
}

public static class PatreonPostsResponseExtensions
{
    /// <summary>Resolves each post's media relationship against the page's included media.</summary>
    public static IEnumerable<PatreonFile> GetFiles(this PatreonPostsResponse page)
    {
        // included only covers this page's posts, so build the lookup per page
        var media = new Dictionary<string, PatreonMediaAttributes>();
        foreach (var resource in page.Included)
        {
            if (resource.Type == "media" && resource.GetAttributes<PatreonMediaAttributes>() is { State: "ready" } attrs)
            {
                media.TryAdd(resource.Id, attrs);
            }
        }

        foreach (var post in page.Data)
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rel in post.Relationships?.Media?.Data ?? [])
            {
                if (!media.TryGetValue(rel.Id, out var m))
                {
                    continue;
                }

                // Generated embed thumbnails have a URL as their file_name
                if (string.IsNullOrEmpty(m.FileName) ||
                    m.FileName.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var url = m.DownloadUrl ?? m.Display?.Url;
                if (url is null)
                {
                    continue;
                }

                var name = $"{post.Id}_{Sanitize(m.FileName)}";
                if (!usedNames.Add(name))
                {
                    name = $"{post.Id}_{rel.Id}_{Sanitize(m.FileName)}"; // duplicate name within a post
                }

                yield return new PatreonFile(post.Id, rel.Id, name, url);
            }
        }
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}