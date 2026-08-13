using System.Security.Cryptography;
using Serilog;

namespace NicheImageRipper.Core.Utility;

/// <summary>
///     A byte-prefix trie for matching file signatures ("magic numbers") to extensions. Structurally guarantees
///     longest-prefix-wins semantics — no manual mask ordering to get wrong when adding a new signature.
/// </summary>
public sealed class FileSignatureTrie
{
    private sealed class Node
    {
        public Dictionary<byte, Node>? Children;
        public string? Extension; // set if a signature terminates at this node
    }

    private readonly Node _root = new();

    public void Add(byte[] signature, string extension)
    {
        var node = _root;
        foreach (var b in signature)
        {
            node.Children ??= new Dictionary<byte, Node>();
            if (!node.Children.TryGetValue(b, out var child))
            {
                child = new Node();
                node.Children[b] = child;
            }

            node = child;
        }

        node.Extension = extension;
    }

    /// <summary>Finds the extension for the longest signature prefix that matches the given bytes.</summary>
    /// <returns>The matched extension, or null if no registered signature matches.</returns>
    public string? Match(byte[] data)
    {
        var node = _root;
        string? lastMatch = null;

        foreach (var b in data)
        {
            if (node.Children is null || !node.Children.TryGetValue(b, out var child))
            {
                break;
            }

            node = child;
            if (node.Extension is not null)
            {
                lastMatch = node.Extension;
            }
        }

        return lastMatch;
    }
}

public static class FileUtility
{
    private static readonly ILogger Logger = Log.ForContext(typeof(FileUtility));

    private static readonly FileSignatureTrie SignatureTrie = BuildSignatureTrie();

    private static FileSignatureTrie BuildSignatureTrie()
    {
        var trie = new FileSignatureTrie();
        trie.Add([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], ".png");
        trie.Add([.. "CSFCHUNK"u8], ".clip");
        trie.Add([.. "<!DOCTYP"u8], ".html");
        trie.Add([.. "<!doctyp"u8], ".html");
        trie.Add([0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00, 0x00], ".rar");
        trie.Add([0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C], ".7z");
        trie.Add([.. "GIF8"u8], ".gif");
        trie.Add([0x50, 0x4B, 0x03, 0x04], ".zip");
        trie.Add([.. "8BPS"u8], ".psd");
        trie.Add([.. "%PDF"u8], ".pdf");
        trie.Add([0x1A, 0x45, 0xDF, 0xA3], ".webm");
        trie.Add([.. "RIFF"u8], ".webp");
        trie.Add([0xFF, 0xD8, 0xFF], ".jpg");
        return trie;
    }

    /// <summary>
    ///     Determines the correct file extension by analyzing the file's signature.
    /// </summary>
    /// <param name="filepath">The path to the file to analyze.</param>
    /// <returns>
    ///     The true extension of the file based on its signature.
    ///     If the file does not have a recognized signature or is too small, the default extension <c>.bin</c> is returned.
    /// </returns>
    public static string GetCorrectExtension(string filepath)
    {
        var data = ReadSignature(filepath, 8);
        if (data is null)
        {
            return ".bin"; // Default extension if reading failed or file is too small
        }

        // MP4 sig starts from offset 4, so handle as special case
        if (data.Length >= 8 && data[4] == 0x66 && data[5] == 0x74 && data[6] == 0x79 && data[7] == 0x70)
        {
            return ".mp4";
        }

        return SignatureTrie.Match(data) ?? ".bin";
    }

    /// <summary>
    ///     Reads a specified number of bytes from the beginning of a file and returns them as a byte array.
    /// </summary>
    /// <param name="filepath">The path to the file to read from.</param>
    /// <param name="length">The number of bytes to read from the file.</param>
    /// <returns>A byte array containing the read bytes, or <c>null</c> if the file is shorter than the specified length.</returns>
    private static byte[]? ReadSignature(string filepath, int length)
    {
        var signature = new byte[length];
        using var stream = File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bytesRead = stream.Read(signature, 0, length);
        return bytesRead < length ? null : signature;
    }

    /// <summary>
    ///     Get the SHA-256 hash of a file.
    /// </summary>
    /// <param name="filepath">The path to the file to hash.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The SHA-256 hash of the file.</returns>
    public static async Task<byte[]> GetFileHash(string filepath, CancellationToken cancellationToken = default)
    {
        await using var stream = await TryOpenFile(filepath, cancellationToken: cancellationToken);
        return await SHA256.HashDataAsync(stream, cancellationToken);
    }

    private static async Task<FileStream> TryOpenFile(string filepath, int maxAttempts = 3, int delayMs = 250,
                                                      CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException e)
            {
                Logger.Debug(e, "Failed to open {Filepath} (attempt {Attempt}/{MaxAttempts}), retrying...",
                    filepath, attempt + 1, maxAttempts);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        throw new IOException($"Failed to open file {filepath} after {maxAttempts} attempts.");
    }

    /// <summary>Checks whether a path is a valid directory path, creating it if it doesn't already exist.</summary>
    /// <param name="path">The path to validate/create.</param>
    /// <returns>True if the path is a valid, existing (or newly-created) directory.</returns>
    public static bool IsValidAndEnsureDirectory(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || Path.GetInvalidPathChars().Any(path.Contains))
            {
                return false;
            }

            if (Directory.Exists(path))
            {
                return true;
            }

            Directory.CreateDirectory(path);
            return true;
        }
        catch (Exception e)
        {
            Logger.Debug(e, "Path is invalid or inaccessible: {Path}", path);
            return false;
        }
    }
}