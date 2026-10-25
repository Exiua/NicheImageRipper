namespace NicheImageRipper.Core.Utility;

public static class CacheUtility
{
    public static async Task<(string, int)> ReadRipIndex(string ripIndexPath, CancellationToken cancellationToken = default)
    {
        var savePosition = await File.ReadAllTextAsync(ripIndexPath, cancellationToken);
        var split = savePosition.Split("|");
        var saveUrl = split[0];
        var start = int.Parse(split[1]);
        return (saveUrl, start);
    }

    public static async Task SaveRipIndex(string ripIndexPath, string saveUrl, int start, CancellationToken cancellationToken = default)
    {
        var position = $"{saveUrl}|{start}";
        await File.WriteAllTextAsync(ripIndexPath, position, cancellationToken);
    }
}