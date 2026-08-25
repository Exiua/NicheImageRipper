using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using ImageMagick;





using OpenQA.Selenium;
using Sdk.Configuration;
using Sdk.DataStructures;
using Sdk.Exceptions;
using Sdk.Utility;

namespace NicheImageRipper.SiteModules.Modules.Pixiv;

public sealed class PixivUgoiraDownloadStrategy : IFileDownloadStrategy
{
    private static GeneralConfig Config => Sdk.Configuration.Config.Instance;

    public IEnumerable<LinkInfo> HandlesLinkInfo => [PixivUgoiraLinkInfo.PixivUgoira];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var illustId = link.Url.Split("/")[4];
        var metadataUrl = $"https://www.pixiv.net/ajax/illust/{illustId}/ugoira_meta";
        context.Logger.Debug("Fetching Pixiv Ugoira metadata from {MetadataUrl}", metadataUrl);

        var cookies = Config.Cookies.GetValueOrDefault(PixivParser.ParserName, []);
        if (cookies.Length == 0)
        {
            throw new RipperException("Pixiv cookies not found in configuration");
        }

        var sessionId = TokenManager.Instance.GetTokenWithRotation(RotationKey.Pixiv, TimeSpan.FromHours(24),
            cookies);
        var driver = context.WebDriver.Driver;
        driver.Url = "https://www.pixiv.net/";
        driver.SetCookie("PHPSESSID", sessionId);
        driver.Url = metadataUrl;
        var rawView = driver.FindElement(By.Id("rawdata-tab"));
        rawView.Click();
        var jsonPre = driver.FindElement(By.XPath("//pre[@class='data']"));
        var json = JsonNode.Parse(jsonPre.Text);
        if (json is null)
        {
            throw new RipperException("Failed to parse Pixiv Ugoira metadata");
        }

        json = json.AsObject();
        if (json["error"].Deserialize<bool>())
        {
            throw new RipperException("Received error while fetching Pixiv Ugoira metadata");
        }

        var oldReferer = context.RequestHeaders[RequestHeaderKeys.Referer];
        context.RequestHeaders[RequestHeaderKeys.Referer] = $"https://www.pixiv.net/artworks/{illustId}";
        var body = json["body"]!.AsObject();
        var keys = new[] { "originalSrc", "src" };
        HttpResponseMessage response = null!;
        List<(string, int)> framesMetadata = null!;

        foreach (var (i, key) in keys.Enumerate())
        {
            var src = body[key]?.GetValue<string>();
            if (src is null)
            {
                context.Logger.Warning("Pixiv Ugoira source not found for key: {Key}", key);
                if (i == keys.Length - 1)
                {
                    context.RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
                    throw new RipperException("Pixiv Ugoira source not found");
                }

                continue;
            }

            var headRequest = context.RequestHeaders.ToRequest(HttpMethod.Head, src);
            response = await context.Session.SendAsync(headRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                context.Logger.Warning("Failed to access Pixiv Ugoira source: {Src}", src);
                if (i == keys.Length - 1)
                {
                    context.RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
                    throw new RipperException("Unable to access Pixiv Ugoira source");
                }

                continue;
            }

            var getRequest = context.RequestHeaders.ToRequest(HttpMethod.Get, src);
            response = await context.Session.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                context.Logger.Warning("Failed to download Pixiv Ugoira: {Src}", src);
                if (i == keys.Length - 1)
                {
                    context.RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
                    throw new RipperException("Unable to download Pixiv Ugoira");
                }

                continue;
            }

            framesMetadata = body["frames"]!
                            .AsArray()
                            .Select(f => (f!["file"]!.GetValue<string>(), f["delay"]!.GetValue<int>()))
                            .OrderBy(f => f.Item1)
                            .ToList();
            break;
        }

        await using var zipStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        using var animation = new MagickImageCollection();
        foreach (var (fileName, delay) in framesMetadata)
        {
            var entry = archive.GetEntry(fileName);
            if (entry is null)
            {
                context.Logger.Warning("Warning: {FileName} not found in ZIP", fileName);
                continue;
            }

            await using var entryStream = await entry.OpenAsync(cancellationToken);
            var img = new MagickImage(entryStream) { AnimationDelay = (uint)(delay / 10) };
            animation.Add(img);
        }

        animation[0].AnimationIterations = 0;
        await animation.WriteAsync(imagePath, cancellationToken);
        context.RequestHeaders[RequestHeaderKeys.Referer] = oldReferer;
        TokenManager.Instance.UpdateTokenRotation(RotationKey.Pixiv);
        return DownloadResult.Success();
    }
}