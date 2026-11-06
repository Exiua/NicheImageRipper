using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NicheImageRipper.SiteModules.Modules.Mega;

public enum MegaLinkKind
{
    /// <summary>A public file link.</summary>
    File,

    /// <summary>A folder link, or a subfolder of one.</summary>
    Folder,

    /// <summary>A file inside a folder link.</summary>
    FolderFile,
}

/// <summary>
///     A parsed mega.nz link. Handles the current formats (<c>/file/&lt;id&gt;#&lt;key&gt;</c>,
///     <c>/folder/&lt;id&gt;#&lt;key&gt;</c>, optionally followed by <c>/file/&lt;handle&gt;</c> or
///     <c>/folder/&lt;handle&gt;</c>) and the legacy ones (<c>#!&lt;id&gt;!&lt;key&gt;</c>,
///     <c>#F!&lt;id&gt;!&lt;key&gt;</c>).
/// </summary>
/// <param name="Kind">What the link points to.</param>
/// <param name="BaseUrl">
///     The public file link, or the folder link without any subfolder/file suffix (what the SDK logs in to).
/// </param>
/// <param name="ChildHandle">Base64 handle of the subfolder or file inside the folder link, if any.</param>
public sealed partial record MegaLink(MegaLinkKind Kind, string BaseUrl, string? ChildHandle)
{
    public static bool TryParse(string url, [NotNullWhen(true)] out MegaLink? link)
    {
        link = null;
        url = url.Trim();

        var match = CurrentFormatRegex().Match(url);
        if (match.Success)
        {
            var id = match.Groups["id"].Value;
            var key = match.Groups["key"].Value;
            if (match.Groups["type"].Value == "file")
            {
                link = new MegaLink(MegaLinkKind.File, $"https://mega.nz/file/{id}#{key}", null);
                return true;
            }

            var baseUrl = $"https://mega.nz/folder/{id}#{key}";
            link = match.Groups["childType"].Value switch
            {
                "file" => new MegaLink(MegaLinkKind.FolderFile, baseUrl, match.Groups["child"].Value),
                "folder" => new MegaLink(MegaLinkKind.Folder, baseUrl, match.Groups["child"].Value),
                _ => new MegaLink(MegaLinkKind.Folder, baseUrl, null),
            };
            return true;
        }

        match = LegacyFormatRegex().Match(url);
        if (match.Success)
        {
            var id = match.Groups["id"].Value;
            var key = match.Groups["key"].Value;
            link = match.Groups["folder"].Success
                ? new MegaLink(MegaLinkKind.Folder, $"https://mega.nz/folder/{id}#{key}", null)
                : new MegaLink(MegaLinkKind.File, $"https://mega.nz/file/{id}#{key}", null);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Link to a file inside this link's folder, in MEGA's own format.
    /// </summary>
    public string FolderFileUrl(string fileHandle)
    {
        return Kind == MegaLinkKind.File
            ? throw new InvalidOperationException("File links have no files inside them.")
            : $"{BaseUrl}/file/{fileHandle}";
    }

    /// <summary>
    ///     Gets the link back in its full form.
    /// </summary>
    public override string ToString()
    {
        return Kind switch
        {
            MegaLinkKind.FolderFile => $"{BaseUrl}/file/{ChildHandle}",
            MegaLinkKind.Folder when ChildHandle is not null => $"{BaseUrl}/folder/{ChildHandle}",
            _ => BaseUrl,
        };
    }

    [GeneratedRegex(
        @"mega\.(?:nz|co\.nz)/(?<type>file|folder)/(?<id>[\w-]+)#(?<key>[\w-]+)(?:/(?<childType>file|folder)/(?<child>[\w-]+))?",
        RegexOptions.IgnoreCase)]
    private static partial Regex CurrentFormatRegex();

    [GeneratedRegex(@"mega\.(?:nz|co\.nz)/#(?<folder>F)?!(?<id>[\w-]+)!(?<key>[\w-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex LegacyFormatRegex();
}
