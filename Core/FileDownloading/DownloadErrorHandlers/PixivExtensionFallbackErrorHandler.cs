using System.Net;
using NicheImageRipper.Core.DataStructures;

namespace NicheImageRipper.Core.FileDownloading.DownloadErrorHandlers;

public sealed class PixivExtensionFallbackErrorHandler : IDownloadErrorHandler
{
    private static readonly Dictionary<string, string> PixivExtMap = new()
    {
        ["jpg"] = "png",
        ["png"] = "gif",
    };

    public bool AppliesTo(HttpStatusCode statusCode, string url, FileLink link, DownloadContext context) =>
        statusCode == HttpStatusCode.NotFound && context.SiteName == "pixiv";

    public Task<DownloadResult> HandleAsync(HttpResponseMessage response, string url, FileLink link,
                                            bool generatingManually, DownloadContext context, CancellationToken cancellationToken)
    {
        // TODO: Improve this — API seems to always return .jpg even if the file is a .png or .gif
        var parts = link.Url.Split(".");
        var ext = parts[^1];
        if (PixivExtMap.TryGetValue(ext, out var mappedExt))
        {
            parts[^1] = mappedExt;
            link.Url = string.Join(".", parts);
            context.Logger.Information("Trying again with .{MappedExt} extension...", mappedExt);
            return Task.FromResult(DownloadResult.Retry());
        }

        // Some images may not exist, so we just log and move on
        // e.g., https://www.pixiv.net/en/artworks/14742347 — not sure if other extensions exist
        context.Logger.Warning("Unable to download Pixiv image: {URL}", link.Url);
        return Task.FromResult(DownloadResult.SkipNotAFailure("Pixiv image not available in any known extension"));
    }
}