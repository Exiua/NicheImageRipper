using System.Text.RegularExpressions;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using Sdk.DataStructures;
using Sdk.FileDownloading;
using Serilog;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

public sealed partial class ResolveImageDownloadStrategy : IFileDownloadStrategy
{
    private const int RetryCount = 4;
    private readonly GenericHttpDownloadStrategy _genericDownload = new();

    public IEnumerable<LinkInfo> HandlesLinkInfo => [LinkInfo.ResolveImage];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                     CancellationToken cancellationToken = default)
    {
        var url = link.Url;
        for (var i = 0; i < RetryCount; i++)
        {
            var imageUrl = await GetDownloadUrl(url, context, cancellationToken);
            if (imageUrl == "")
            {
                await Task.Delay(500, cancellationToken);
                continue;
            }

            context.Logger.Debug("Resolved URL: {Url}", imageUrl);
            link.Url = imageUrl;

            // Original delegated actual file writing to the generic download path (its own internal retry loop
            // included) — reusing GenericHttpDownloadStrategy here preserves the nested-retry behavior exactly.
            var result = await _genericDownload.DownloadAsync(link, imagePath, context, cancellationToken);
            if (result.Outcome == DownloadOutcome.Success)
            {
                return result;
            }

            await Task.Delay(500, cancellationToken);
        }

        return DownloadResult.Failed();
    }

    private static async Task<string> GetDownloadUrl(string url, DownloadContext context,
                                                      CancellationToken cancellationToken)
    {
        var siteName = url.Split('.')[1];
        using var request = context.RequestHeaders.ToRequest(HttpMethod.Get, url);
        var response = await context.Session.SendAsync(request, HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            context.Logger.Error("<Response {ErrorCode}> Failed to get download url: {Url}", response.StatusCode, url);
            return "";
        }

        if (response.RequestMessage!.RequestUri!.ToString() == $"https://www.{siteName}.com/hcaptcha.aspx")
        {
            context.Logger.Information("Captcha detected, solving...");
            await SolveCaptcha(url, true, cancellationToken);
            var reRequest = context.RequestHeaders.ToRequest(HttpMethod.Get, url);
            response = await context.Session.SendAsync(reRequest, HttpCompletionOption.ResponseContentRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                context.Logger.Error("Failed to get download url: {Url}", url);
                return "";
            }
        }

        context.Logger.Information("Getting download url from {Url}", url);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var match = NLegsImageUrlRegex().Match(content);
        return $"https://www.{siteName}.com" + match.Groups[1].Value;
    }

    private static async Task SolveCaptcha(string url, bool humanSolve, CancellationToken cancellationToken)
    {
        await NicheImageRipper.FlareSolverrManager.GetSiteSolution(url, cancellationToken: cancellationToken);
        if (humanSolve)
        {
            Log.Information("Solve the captcha and press enter to continue");
            Console.ReadLine();
        }
    }

    [GeneratedRegex("""
                    <img.+src="([^"]+)"
                    """)]
    private static partial Regex NLegsImageUrlRegex();
}