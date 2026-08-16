using System.Text;
using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.SiteModules.Modules.PixelDrain;

public sealed class PixelDrainDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [PixelDrainLinkInfo.PixelDrain];

    private static GeneralConfig Config => IFileDownloadStrategy.Config;

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var apiKey = Config.Keys.GetValueOrDefault("pixeldrain");
        var base64Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{apiKey}"));
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add(RequestHeaderKeys.UserAgent, Config.UserAgent);
        client.DefaultRequestHeaders.Add(RequestHeaderKeys.Authorization, $"Basic {base64Auth}");

        var response = await client.GetAsync($"https://pixeldrain.com/api/file/{link.Url}",
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return DownloadResult.Failed($"Status code: {response.StatusCode}");
        }

        await using var fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fileStream, cancellationToken);
        return DownloadResult.Success();
    }
}