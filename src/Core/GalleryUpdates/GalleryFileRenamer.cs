using NicheImageRipper.Sdk.Common.ExtensionMethods;
using Serilog;

namespace NicheImageRipper.Core.GalleryUpdates;

/// <summary>Moves a gallery's existing files to the names a <see cref="GalleryUpdatePlan"/> gives them.</summary>
public static class GalleryFileRenamer
{
    private static readonly ILogger Logger = Log.ForContext(typeof(GalleryFileRenamer));

    /// <summary>
    ///     Renames in two passes (everything to a temporary name, then to its target), since targets usually overlap
    ///     other sources (inserting at the start shifts 0→1, 1→2, ...). Missing sources are skipped, e.g. files the
    ///     user deleted or duplicates removed after download. A target held by a file the gallery doesn't track (such
    ///     as a failed download from an earlier rip) is overwritten.
    /// </summary>
    public static void Apply(string directory, IReadOnlyList<GalleryFileRename> renames)
    {
        var batch = Guid.NewGuid().ToString("N");
        var moved = new List<(string Temp, string Target)>();
        foreach (var (i, rename) in renames.Enumerate())
        {
            var source = Path.Combine(directory, rename.From);
            if (!File.Exists(source) && !Directory.Exists(source))
            {
                Logger.Debug("Skipping rename of missing file {From}", rename.From);
                continue;
            }

            var temp = Path.Combine(directory, $".rename-{batch}-{i}");
            Move(source, temp);
            moved.Add((temp, Path.Combine(directory, rename.To)));
        }

        foreach (var (temp, target) in moved)
        {
            if (Directory.Exists(temp))
            {
                if (Directory.Exists(target) || File.Exists(target))
                {
                    Logger.Warning("Couldn't rename {Temp} to {Target}: the target already exists", temp, target);
                    continue;
                }

                Directory.Move(temp, target);
            }
            else
            {
                File.Move(temp, target, overwrite: true);
            }
        }

        Logger.Debug("Renamed {Count} existing files", moved.Count);
    }

    private static void Move(string source, string destination)
    {
        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
        }
        else
        {
            File.Move(source, destination);
        }
    }
}
