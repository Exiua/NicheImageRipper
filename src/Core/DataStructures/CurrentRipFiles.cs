namespace NicheImageRipper.Core.DataStructures;

/// <summary>The files the rip in progress (or the last one, if it stopped part way) will download, in download order.</summary>
/// <param name="Url">URL being ripped, or that was being ripped when it stopped; null when there's nothing to show.</param>
/// <param name="DirectoryName">Folder the files are saved to, or null when nothing has been parsed yet.</param>
/// <param name="Stopped">Whether the rip errored or was interrupted before finishing, rather than still running.</param>
public record CurrentRipFiles(string? Url, string? DirectoryName, bool Stopped, List<RipFileEntry> Files)
{
    public static CurrentRipFiles Empty => new(null, null, false, []);
}

/// <param name="Downloaded">Whether the file finished downloading (or was already done when the rip resumed).</param>
public record RipFileEntry(string Url, string Filename, bool Downloaded);
