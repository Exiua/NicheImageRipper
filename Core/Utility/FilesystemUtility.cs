using System.Net;
using System.Text;
using Serilog;

namespace NicheImageRipper.Core.Utility;

public static class FilesystemUtility
{
    private static readonly HashSet<char> ForbiddenChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*', '\r', '\n'];
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

        if (cleanedPathStem.Length == 0)
        {
            return "";
        }

        if (!AllowedRightPunctuation.Contains(cleanedPathStem[^1]))
        {
            RStripPunctuation(cleanedPathStem);
        }

        if (cleanedPathStem.Length > 0 && !AllowedLeftPunctuation.Contains(cleanedPathStem[0]))
        {
            LStripPunctuation(cleanedPathStem);
        }

        return cleanedPathStem.ToString();
    }

    /// <summary>
    ///     Clean a directory name by running it through <see cref="CleanPathStem"/>, falling back to a random guid
    ///     if the input is blank or cleans down to nothing, and truncating if it exceeds <paramref name="maxLength"/>.
    /// </summary>
    /// <param name="directoryName">The raw directory name to clean</param>
    /// <param name="maxLength">Maximum allowed length; longer names are truncated</param>
    /// <returns>The cleaned (and possibly truncated or guid-replaced) directory name</returns>
    public static string CleanDirectoryName(string directoryName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(directoryName))
        {
            return Guid.NewGuid().ToString();
        }

        var name = CleanPathStem(directoryName);
        if (name == "")
        {
            Logger.Warning("Directory name was cleaned to empty: {DirectoryName}", directoryName);
            return Guid.NewGuid().ToString();
        }

        if (name.Length <= maxLength)
        {
            return name;
        }

        Logger.Warning("Directory name too long (length: {Length}). Truncating to {MaxLength} characters.",
            name.Length, maxLength);
        return name[..maxLength].Trim();
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