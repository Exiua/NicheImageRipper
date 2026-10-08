using System.Text;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.FileDownloading;
using NicheImageRipper.Sdk.Utility;


namespace NicheImageRipper.SiteModules.Modules.PixelDrain;

public sealed class PixelDrainDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [PixelDrainLinkInfo.PixelDrain];

    private static GeneralConfig Config => IFileDownloadStrategy.Config;

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var apiKey = Config.GetSiteConfig(PixelDrainParser.ParserName)?.Key ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("PixelDrain API key is not set in the configuration.");
        }
        
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