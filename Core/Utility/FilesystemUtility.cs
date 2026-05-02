using System.Net;
using System.Text;
using Serilog;

namespace Core.Utility;

public static class FilesystemUtility
{
    private static readonly HashSet<char> ForbiddenChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*' , '\r', '\n'];
    private static readonly HashSet<char> AllowedRightPunctuation = [')', ']', '}', '】', '»', '“', '”', '’', '」', '』'];
    private static readonly HashSet<char> AllowedLeftPunctuation = ['(', '[', '{', '【', '«', '„', '“', '‘', '「', '『'];
    private static readonly ILogger Logger = Log.ForContext(typeof(FilesystemUtility)); 
    
    /// <summary>
    ///     Clean a path name by removing forbidden characters and trimming whitespace and punctuation. Does not modify
    ///     valid enclosing characters such as parentheses, brackets, or braces.
    /// </summary>
    /// <param name="pathName">The path name to clean</param>
    /// <returns>>The cleaned path name</returns>
    public static string CleanPathStem(string pathName)
    {
        if (pathName == "")
        {
            return "";
        }
        
        Logger.Debug("Cleaning path stem for \"{PathName}\"", pathName);
        pathName = WebUtility.HtmlDecode(pathName).Trim(' ', '\t', '\n', '\r');
        var cleanedPathStem = new StringBuilder();
        foreach (var c in pathName.Where(c => !ForbiddenChars.Contains(c)))
        {
            cleanedPathStem.Append(c);
        }

        if (!AllowedRightPunctuation.Contains(cleanedPathStem[^1]))
        {
            RStripPunctuation(cleanedPathStem);
        }

        if (!AllowedLeftPunctuation.Contains(cleanedPathStem[0]))
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