using NicheImageRipper.Sdk.Cache;

namespace NicheImageRipper.Core.Cache;

internal sealed class RipStateCacheOwner : ICacheOwner
{
    public static void ClearCache()
    {
        SilentlyRemoveFiles(".ripIndex", "ripState.json");
    }

    private static void SilentlyRemoveFiles(params string[] filepaths)
    {
        foreach (var filepath in filepaths)
        {
            try
            {
                File.Delete(filepath);
            }
            catch (FileNotFoundException)
            {
                // ignored
            }
        }
    }
}