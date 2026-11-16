using System.Xml.Linq;
using Serilog;
using SteamKit2;
using SteamKit2.Internal;

namespace SteamApiClient;

public sealed class SteamApiClient : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<SteamApiClient>();
    private static readonly HttpClient Http = new();
    private readonly SteamSession _session;
    private readonly DepotDownloader _depotDownloader;

    public SteamApiClient() : this(new SteamApiClientOptions())
    {
    }

    public SteamApiClient(SteamApiClientOptions options)
    {
        _session = new SteamSession(options);
        _depotDownloader = new DepotDownloader(_session, options);
    }

    /// <summary>
    /// Logs on, or joins the logon already in progress for the same user. Completes immediately if already logged on.
    /// </summary>
    public Task LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
        _session.LoginAsync(username, password, cancellationToken);

    public Task LogoutAsync(CancellationToken cancellationToken = default) => _session.LogoutAsync();

    public Task ReconnectAsync(CancellationToken cancellationToken = default) =>
        _session.ReconnectAsync(cancellationToken);

    public Task DownloadWorkshopFileAsync(ulong publishedFileId, string outputBaseDirectory,
                                          CancellationToken cancellationToken = default) =>
        RunLoggedOnAsync(async () =>
        {
            Logger.Information("Downloading workshop file {PublishedFileId}", publishedFileId);
            var service = _session.UnifiedMessages.CreateService<PublishedFile>();
            var response = await service.GetDetails(new CPublishedFile_GetDetails_Request
            {
                publishedfileids = { publishedFileId }, includeadditionalpreviews = true, includechildren = true,
                short_description = true, strip_description_bbcode = false,
            }).ToTask().WaitAsync(cancellationToken);
            EnsureOk(response.Result, $"get details for workshop file {publishedFileId}");
            var details = response.Body.publishedfiledetails.SingleOrDefault() ??
                          throw new SteamApiException($"Workshop file {publishedFileId} was not found.");
            EnsureOk((EResult)details.result, $"get details for workshop file {publishedFileId}");
            Logger.Information(
                "Workshop item: {Title}, App: {ConsumerAppId}, File: {FileName}, Size: {Size}, HFile: {HFile}",
                details.title, details.consumer_appid, details.filename, details.file_size, details.hcontent_file);

            var outputPath = Path.Combine(outputBaseDirectory, publishedFileId.ToString());
            if (!string.IsNullOrEmpty(details.file_url))
            {
                Logger.Information("File has direct URL, downloading directly...");
                // details.filename is user-controlled, so only its last segment is used
                var fileName = Path.GetFileName(details.filename);
                await DownloadDirectAsync(details.file_url,
                    Path.Combine(outputPath, string.IsNullOrEmpty(fileName) ? publishedFileId.ToString() : fileName),
                    cancellationToken);
                return;
            }

            // Workshop content lives in a depot with the same id as the consumer app
            var target = await _depotDownloader.ResolveTargetAsync(details.consumer_appid, details.consumer_appid,
                details.hcontent_file, cancellationToken);
            await _depotDownloader.DownloadAsync(target, outputPath, cancellationToken);
        }, cancellationToken);

    private static async Task DownloadDirectAsync(string url, string outputPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using var input = await Http.GetStreamAsync(url, cancellationToken);
        await using var output = File.Create(outputPath);
        await input.CopyToAsync(output, cancellationToken);
    }

    public Task<IReadOnlyList<PublishedFileDetails>> GetUserWorkshopItemsAsync(
        ulong steamId, uint appId, CancellationToken cancellationToken = default) =>
        RunLoggedOnAsync<IReadOnlyList<PublishedFileDetails>>(async () =>
        {
            Logger.Information("Getting workshop items for user {SteamId}, app {AppId}", steamId, appId);
            var service = _session.UnifiedMessages.CreateService<PublishedFile>();
            const uint pageSize = 100;
            var results = new List<PublishedFileDetails>();
            for (uint page = 1;; page++)
            {
                var response = await service.GetUserFiles(new CPublishedFile_GetUserFiles_Request
                {
                    steamid = steamId, appid = appId, page = page, numperpage = pageSize,
                    sortmethod = "lastupdated",
                }).ToTask().WaitAsync(cancellationToken);
                EnsureOk(response.Result, $"list workshop items for {steamId}");
                var files = response.Body.publishedfiledetails;
                if (files.Count == 0)
                {
                    break;
                }

                results.AddRange(files);
                Logger.Information("Fetched page {Page}, got {Count} items, total so far {Total}", page, files.Count,
                    results.Count);
                if (results.Count >= response.Body.total)
                {
                    break;
                }
            }

            Logger.Information("Found {Total} workshop items for user {SteamId}, app {AppId}", results.Count, steamId,
                appId);
            return results;
        }, cancellationToken);

    public Task<string> GetPersonaNameAsync(ulong steamId, CancellationToken cancellationToken = default) =>
        RunLoggedOnAsync(async () =>
        {
            Logger.Information("Getting persona name for {SteamId}", steamId);
            var service = _session.UnifiedMessages.CreateService<Player>();
            var response = await service.GetPlayerLinkDetails(new CPlayer_GetPlayerLinkDetails_Request
                { steamids = { steamId } }).ToTask().WaitAsync(cancellationToken);
            EnsureOk(response.Result, $"get persona name for {steamId}");
            var name = response.Body.accounts.FirstOrDefault()?.public_data?.persona_name;
            if (string.IsNullOrEmpty(name))
            {
                Logger.Warning("Persona name was empty for SteamID {SteamId}", steamId);
                return steamId.ToString();
            }

            Logger.Information("Resolved persona name: {Name}", name);
            return name;
        }, cancellationToken);

    public static async Task<ulong> ResolveVanityUrlAsync(string vanityUrl,
                                                          CancellationToken cancellationToken = default)
    {
        Logger.Information("Resolving vanity URL {VanityUrl}", vanityUrl);
        using var response = await Http.GetAsync(
            $"https://steamcommunity.com/id/{Uri.EscapeDataString(vanityUrl)}/?xml=1", cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var root = XDocument.Parse(content).Root;
        if (!ulong.TryParse(root?.Element("steamID64")?.Value, out var steamId))
        {
            var error = root?.Element("error")?.Value ?? "profile not found";
            throw new SteamApiException($"Failed to resolve vanity URL '{vanityUrl}': {error}");
        }

        Logger.Information("Resolved {VanityUrl} -> {SteamId}", vanityUrl, steamId);
        return steamId;
    }

    /// <summary>
    /// Waits for the session to be logged on, then runs <paramref name="action"/>. If Steam reports the session as
    /// no longer valid, a reconnect is started (later calls wait for it) and the original error is rethrown.
    /// </summary>
    private async Task<T> RunLoggedOnAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _session.EnsureLoggedOnAsync(cancellationToken);
        try
        {
            return await action();
        }
        catch (AsyncJobFailedException)
        {
            Logger.Warning("Steam session is no longer valid, reconnecting...");
            try
            {
                _ = _session.ReconnectAsync(CancellationToken.None)
                            .ContinueWith(t => Logger.Warning(t.Exception, "Reconnect to Steam failed"),
                                TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception e)
            {
                // e.g. logged out concurrently; don't let this mask the original failure
                Logger.Warning(e, "Could not start Steam reconnect");
            }

            throw;
        }
    }

    private Task RunLoggedOnAsync(Func<Task> action, CancellationToken cancellationToken) =>
        RunLoggedOnAsync(async () =>
        {
            await action();
            return true;
        }, cancellationToken);

    private static void EnsureOk(EResult result, string operation)
    {
        if (result != EResult.OK)
        {
            throw new SteamApiException($"Failed to {operation}: {result}", result);
        }
    }

    public void Dispose()
    {
        _depotDownloader.Dispose();
        _session.Dispose();
    }
}
