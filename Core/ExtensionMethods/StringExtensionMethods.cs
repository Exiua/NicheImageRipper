using System.Web;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Core.ExtensionMethods;

public static class StringExtensionMethods
{
    extension(string s)
    {
        public string ToQueryString(Dictionary<string, string> dict)
        {
            var uriBuilder = new UriBuilder(s);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            foreach (var (key, value) in dict)
            {
                query[key] = value;
            }
            uriBuilder.Query = query.ToString();
            return uriBuilder.ToString();
        }

        public string Join(IEnumerable<string> values)
        {
            return string.Join(s, values);
        }
        
        public bool Contains(params string[] substrings)
        {
            return substrings.All(s.Contains);
        }

        public string TrimStartMatches(string prefix)
        {
            return s.StartsWith(prefix) ? s[prefix.Length..] : s;
        }
    }
}