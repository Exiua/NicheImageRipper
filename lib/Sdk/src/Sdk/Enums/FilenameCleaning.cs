namespace NicheImageRipper.Sdk.Enums;

/// <summary>
///     How a filename supplied by a parser is cleaned (see <see cref="Utility.FilesystemUtility.CleanPathStem" />).
/// </summary>
public enum FilenameCleaning
{
    /// <summary>Use the filename as given.</summary>
    None = 0,

    /// <summary>
    ///     Clean each segment of a relative path (e.g. <c>sub/dir/file.jpg</c>), keeping the subdirectories. The result
    ///     can't escape the download directory.
    /// </summary>
    PreserveSubdirectories = 1,

    /// <summary>Clean the whole filename, removing path separators too.</summary>
    Full = 2,
}
