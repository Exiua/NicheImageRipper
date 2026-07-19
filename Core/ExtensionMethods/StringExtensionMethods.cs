using System.Web;
using NicheImageRipper.Core.SiteParsing;

namespace NicheImageRipper.Core.ExtensionMethods;

public static class StringExtensionMethods
{
    extension(string s)
    {
        public IEnumerable<(int i, char)> Enumerate(int start = 0)
        {
            for(var i = start; i < s.Length; i++)
            {
                yield return (i, s[i]);
            }
        }

        public string ToTitle()
        {
            return s[0].ToString().ToUpper() + s[1..];
        }

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

        public List<StringFileLinkWrapper> IntoStringImageLinkWrapperList()
        {
            return [new StringFileLinkWrapper(s)];
        }
        
        public int ParseInt()
        {
            return int.Parse(s);
        }
        
        /// <summary>
        ///     Remove HTML artifacts from a url string (such as &amp;) and replace them with their proper characters
        /// </summary>
        /// <returns>Cleaned URL string</returns>
        public string DecodeUrl()
        {
            return HttpUtility.HtmlDecode(s);
        }
        
        /// <summary>
        ///     Remove percent-encoding from a url string.
        /// </summary>
        /// <returns>Cleaned URL string</returns>
        public string UnescapeUrl()
        {
            return Uri.UnescapeDataString(s);
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

    public static string JoinWith(this IEnumerable<string> values, string separator)
    {
        return string.Join(separator, values);
    }
}