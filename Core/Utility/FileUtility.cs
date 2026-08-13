using System.Security.Cryptography;
using Serilog;

namespace NicheImageRipper.Core.Utility;

public static class FileUtility
{
    private static readonly ILogger Logger = Log.ForContext(typeof(FileUtility));

    private static readonly Dictionary<ulong, string> FileSignatures = new()
    {
        [0x89_50_4E_47_0D_0A_1A_0A] = ".png", // /8
        [0x43_53_46_43_48_55_4E_4B] = ".clip", // /8
        [0x3C_21_44_4F_43_54_59_50] = ".html", // /8 // <!DOCTYP
        [0x3C_21_64_6F_63_74_79_70] = ".html", // /8 // <!doctyp
        [0x52_61_72_21_1A_07_00_00] = ".rar", // /6
        [0x37_7A_BC_AF_27_1C_00_00] = ".7z", // /6
        [0x47_49_46_38_00_00_00_00] = ".gif", // /4
        [0x50_4B_03_04_00_00_00_00] = ".zip", // /4
        [0x38_42_50_53_00_00_00_00] = ".psd", // /4
        [0x25_50_44_46_00_00_00_00] = ".pdf", // /4
        [0x1A_45_DF_A3_00_00_00_00] = ".webm", // /4
        [0x52_49_46_46_00_00_00_00] = ".webp", // /4
        [0x00_00_00_00_66_74_79_70] = ".mp4", // /4, bytes 4-7 ("ftyp" box type — MP4's signature isn't at offset 0)
        [0xFF_D8_FF_00_00_00_00_00] = ".jpg", // /3
    };

    // Tried in this order; a signature entry's length must be listed here so its mask is checked before any
    // shorter/less-specific mask that could otherwise match a false positive first.
    private static readonly ulong[] SignatureMasks =
    [
        0xFFFF_FFFF_FFFF_FFFF, // 8 bytes
        0xFFFF_FFFF_FFFF_0000, // 6 bytes
        0xFFFF_FFFF_0000_0000, // 4 bytes
        0x0000_0000_FFFF_FFFF, // 4 bytes, offset 4 (e.g. mp4's ftyp box type)
        0xFFFF_FF00_0000_0000, // 3 bytes
    ];

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
        var signature = ReadSignature(filepath, 8);
        if (signature is null)
        {
            return ".bin"; // Default extension if reading failed or file is too small
        }

        var signatureInt = ToUInt64(signature, 0);
        foreach (var mask in SignatureMasks)
        {
            var maskedSignature = signatureInt & mask;
            if (FileSignatures.TryGetValue(maskedSignature, out var ext))
            {
                return ext;
            }
        }

        return ".bin"; // Default extension if no matching signature is found
    }

    /// <summary>
    ///     Converts eight bytes from a byte array, starting at a specified index, into a 64-bit unsigned integer.
    /// </summary>
    /// <param name="bytes">The byte array containing the bytes to convert.</param>
    /// <param name="startIndex">The starting index within the byte array.</param>
    /// <returns>A 64-bit unsigned integer representing the value of the eight bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown if <paramref name="startIndex"/> is less than 0 or if there are fewer than eight bytes remaining from the specified index.
    /// </exception>
    private static ulong ToUInt64(byte[] bytes, int startIndex)
    {
        var value = 0UL;
        for (var i = 0; i < 8; i++)
        {
            value |= (ulong)bytes[startIndex + i] << (8 * (7 - i));
        }

        return value;
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