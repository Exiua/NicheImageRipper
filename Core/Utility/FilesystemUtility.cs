using System.Net;
using System.Text;

namespace Core.Utility;

public static class FilesystemUtility
{
    private static readonly HashSet<char> ForbiddenChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];
    
    public static string CleanPathStem(string pathStem)
    {
        pathStem = WebUtility.HtmlDecode(pathStem).Trim(' ', '\t', '\n', '\r');
        var cleanedPathStem = new StringBuilder();
        foreach (var c in pathStem.Where(c => !ForbiddenChars.Contains(c)))
        {
            cleanedPathStem.Append(c);
        }

        if (cleanedPathStem[^1] != ')' && cleanedPathStem[^1] != ']' && cleanedPathStem[^1] != '}')
        {
            RStripPunctuation(cleanedPathStem);
        }

        if (cleanedPathStem[0] != '(' && cleanedPathStem[0] != '[' && cleanedPathStem[0] != '{')
        {
            LStripPunctuation(cleanedPathStem);
        }

        return cleanedPathStem.ToString();
    }
    
    private static void LStripPunctuation(StringBuilder input)
    {
        if (input.Length == 0)
        {
            return;
        }

        var i = 0;
        while (i < input.Length && char.IsPunctuation(input[i]))
        {
            i++;
        }

        if (i > 0)
        {
            input.Remove(0, i); // Remove leading punctuation
        }
    }

    private static void RStripPunctuation(StringBuilder input)
    {
        if (input.Length == 0)
        {
            return;
        }

        var i = input.Length - 1;
        while (i >= 0 && char.IsPunctuation(input[i]))
        {
            i--;
        }

        if (i < input.Length - 1)
        {
            input.Length = i + 1; // Adjust the length to trim the punctuation
        }
    }
}