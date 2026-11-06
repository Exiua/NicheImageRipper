using MegaSdk;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;

namespace NicheImageRipper.SiteModules.Modules.Mega;

public static class MegaHelper
{
    /// <summary>
    ///     Expands a Mega link into one <see cref="FileLink" /> per file. Files in folders keep their path relative to
    ///     the linked folder as their filename (e.g. <c>sub/file.jpg</c>) and get MEGA's per-file link
    ///     (<c>/folder/&lt;id&gt;#&lt;key&gt;/file/&lt;handle&gt;</c>) as their URL.
    /// </summary>
    public static async Task<(List<FileLink>, int)> ExpandAsync(string url, int index, FilenameScheme filenameScheme,
                                                                CancellationToken cancellationToken = default)
    {
        var link = Parse(url);
        MegaClientProvider.EnsureAvailable();

        if (link.Kind == MegaLinkKind.File)
        {
            var client = await MegaClientProvider.GetSharedClientAsync(cancellationToken);
            using var node = await client.GetPublicNodeAsync(link.BaseUrl, cancellationToken);
            return ([CreateFileLink(link.BaseUrl, node.Name, filenameScheme, index)], index + 1);
        }

        var files = await MegaClientProvider.WithFolderSessionAsync(link.BaseUrl,
            (client, root) => ListFiles(client, root, link), cancellationToken);

        var fileLinks = new List<FileLink>(files.Count);
        foreach (var (path, handle) in files)
        {
            fileLinks.Add(CreateFileLink(link.FolderFileUrl(handle), path, filenameScheme, index));
            index++;
        }

        return (fileLinks, index);
    }

    /// <summary>
    ///     Name of what the link points to (the file's or folder's name in MEGA).
    /// </summary>
    public static async Task<string> GetNameAsync(string url, CancellationToken cancellationToken = default)
    {
        var link = Parse(url);
        MegaClientProvider.EnsureAvailable();

        if (link.Kind == MegaLinkKind.File)
        {
            var client = await MegaClientProvider.GetSharedClientAsync(cancellationToken);
            using var node = await client.GetPublicNodeAsync(link.BaseUrl, cancellationToken);
            return node.Name;
        }

        return await MegaClientProvider.WithFolderSessionAsync(link.BaseUrl, (client, root) =>
        {
            using var target = FindTarget(client, root, link);
            return target.Name;
        }, cancellationToken);
    }

    /// <summary>
    ///     Gets a node for a file or folder in a folder link that the shared client can download. The caller owns it.
    /// </summary>
    public static Task<MegaNode?> AuthorizeAsync(MegaLink link, CancellationToken cancellationToken = default)
    {
        return MegaClientProvider.WithFolderSessionAsync(link.BaseUrl, (client, root) =>
        {
            if (link.ChildHandle is null)
            {
                return client.AuthorizeNode(root);
            }

            using var node = client.GetNodeByHandle(link.ChildHandle);
            return node is null ? null : client.AuthorizeNode(node);
        }, cancellationToken);
    }

    /// <summary>
    ///     Whether the error means the link's file or folder is gone: deleted (<see cref="MegaErrorCode.NotFound" />),
    ///     past its expiry date (<see cref="MegaErrorCode.Expired" />), or taken down for breaching MEGA's terms
    ///     (<see cref="MegaErrorCode.Blocked" />).
    /// </summary>
    public static bool IsUnavailable(MegaErrorCode errorCode)
    {
        // TODO: MEGA also reports taken-down files/accounts as TooMany ("Terms of Service breached" for downloads),
        // but outside downloads TooMany can mean too many concurrent connections, so a working link could be treated
        // as gone. Left out until it's confirmed which one a dead link's lookup (getPublicNode/loginToFolder) returns.
        return errorCode is MegaErrorCode.NotFound or MegaErrorCode.Expired or MegaErrorCode.Blocked;
    }

    public static MegaLink Parse(string url)
    {
        return MegaLink.TryParse(url, out var link)
            ? link
            : throw new RipperException($"Unrecognized Mega link: {url}");
    }

    private static FileLink CreateFileLink(string url, string filename, FilenameScheme filenameScheme, int index)
    {
        // Names come from whoever shared the files: clean them, keeping the folder structure inside the gallery
        return FileLink.WithFilename(url, filename, filenameScheme, index: index, linkInfo: MegaLinkInfo.Mega,
            filenameCleaning: FilenameCleaning.PreserveSubdirectories);
    }

    /// <summary>
    ///     The linked folder (the folder link's root, or a subfolder of it) or file.
    /// </summary>
    private static MegaNode FindTarget(MegaClient client, MegaNode root, MegaLink link)
    {
        if (link.ChildHandle is null)
        {
            return client.GetNodeByHandle(root.Handle)
                   ?? throw new RipperException($"Mega folder has no root node: {link}");
        }

        return client.GetNodeByHandle(link.ChildHandle)
               ?? throw new RipperException($"Mega link points to a missing file or folder: {link}");
    }

    /// <summary>
    ///     Relative paths and base64 handles of all files under the link's target, depth first, in MEGA's order.
    /// </summary>
    private static List<(string Path, string Handle)> ListFiles(MegaClient client, MegaNode root, MegaLink link)
    {
        var files = new List<(string, string)>();
        using var target = FindTarget(client, root, link);
        if (target.IsFile)
        {
            files.Add((target.Name, target.Base64Handle));
            return files;
        }

        AddFiles(client, target, "", files);
        return files;
    }

    private static void AddFiles(MegaClient client, MegaNode folder, string path, List<(string, string)> files)
    {
        foreach (var child in client.GetChildren(folder))
        {
            using (child)
            {
                var childPath = path.Length == 0 ? child.Name : $"{path}/{child.Name}";
                if (child.IsFolder)
                {
                    AddFiles(client, child, childPath, files);
                }
                else
                {
                    files.Add((childPath, child.Base64Handle));
                }
            }
        }
    }
}
