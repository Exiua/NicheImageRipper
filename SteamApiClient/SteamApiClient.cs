using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using NicheImageRipper.Common.Exceptions;
using NicheImageRipper.Common.ExtensionMethods;
using Serilog;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;

namespace SteamApiClient;

public class SteamApiClient
{
    private static readonly ILogger Logger = Log.ForContext<SteamApiClient>();
    private static readonly HttpClient Http = new();
    private readonly SteamClient _steamClient;
    private readonly CallbackManager _manager;
    private readonly SteamUser _steamUser;
    private readonly SteamApps _steamApps;
    private readonly SteamUnifiedMessages _steamUnifiedMessages;
    private readonly SteamKit2.CDN.Client _cdnClient;
    private readonly TaskCompletionSource _loginTcs = new();
    private TaskCompletionSource _reconnectTcs = new();

    private readonly
        ConcurrentDictionary<(uint DepotId, string Host), (TaskCompletionSource<string> Tcs, long ExpiryUnix)>
        _cdnAuthTokens = new();

    private string _username = "";
    private string _password = "";
    private string? _previouslyStoredGuardData;

    public SteamApiClient()
    {
        _steamClient = new SteamClient();
        _manager = new CallbackManager(_steamClient);
        _steamUser = _steamClient.GetHandler<SteamUser>()!;
        _steamApps = _steamClient.GetHandler<SteamApps>()!;
        _steamUnifiedMessages = _steamClient.GetHandler<SteamUnifiedMessages>()!;
        _cdnClient = new SteamKit2.CDN.Client(_steamClient);
        _manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
    }

    public async Task LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (_username == username)
        {
            // _username can only be set via this method, so if set to provided username, must already be logged in
            Logger.Debug("Already logged in");
            return;
        }

        Logger.Debug("Logging in as {Username}", username);
        _username = username;
        _password = password;
        _steamClient.Connect();
        await Task.Run(() =>
        {
            while (!_loginTcs.Task.IsCompleted)
            {
                _manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }, cancellationToken);
        // Propagate any login exception
        await _loginTcs.Task;
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        Logger.Information("Logging out");
        _steamClient.Disconnect();
        _username = "";
        _password = "";
        return Task.CompletedTask;
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result == EResult.TryAnotherCM)
        {
            Logger.Warning("Steam requested another CM; reconnecting...");
            _steamClient.Disconnect();
            return;
        }

        if (callback.Result != EResult.OK)
        {
            Logger.Error("Unable to logon to Steam: {Result} / {ExtendedResult}", callback.Result,
                callback.ExtendedResult);
            var ex = new InvalidOperationException($"Steam login failed: {callback.Result}");
            if (!_loginTcs.Task.IsCompleted)
            {
                _loginTcs.SetException(ex);
            }
            else if (!_reconnectTcs.Task.IsCompleted)
            {
                _reconnectTcs.SetException(ex);
            }

            return;
        }

        Logger.Information("Successfully logged on!");
        if (!_loginTcs.Task.IsCompleted)
        {
            _loginTcs.SetResult();
        }
        else if (!_reconnectTcs.Task.IsCompleted)
        {
            _reconnectTcs.SetResult();
        }
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        Logger.Information("Disconnected from Steam");
        if (!_loginTcs.Task.IsCompleted)
        {
            _loginTcs.SetException(new InvalidOperationException("Disconnected before login completed."));
            return;
        }

        // Reconnect attempt — OnConnected will fire and re-authenticate
        _steamClient.Connect();
    }

    private async Task<string?> GetCdnAuthTokenAsync(uint depotId, string host,
                                                     CancellationToken cancellationToken = default)
    {
        var key = (depotId, host);
        // If we have a valid non-expired token, return it
        if (_cdnAuthTokens.TryGetValue(key, out var existing) &&
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() < existing.ExpiryUnix - 60)
        {
            return await existing.Tcs.Task;
        }

        // Remove stale/expired entry if present
        _cdnAuthTokens.TryRemove(key, out _);
        var tcs = new TaskCompletionSource<string>();
        var placeholder = (tcs, ExpiryUnix: long.MaxValue); // placeholder expiry until we know the real one
        if (!_cdnAuthTokens.TryAdd(key, placeholder))
        {
            // Lost the race to another thread - use theirs
            return await _cdnAuthTokens[key].Tcs.Task;
        }

        try
        {
            Logger.Debug("Requesting CDN auth token for depot {DepotId} host {Host}", depotId, host);
            var contentService = _steamUnifiedMessages.CreateService<ContentServerDirectory>();
            var response = await contentService.GetCDNAuthToken(new CContentServerDirectory_GetCDNAuthToken_Request
                { depot_id = depotId, host_name = host, app_id = depotId, });
            if (response.Result != EResult.OK)
            {
                Logger.Warning("CDN auth token request failed for {Host}: {Result}", host, response.Result);
                _cdnAuthTokens.TryRemove(key, out _);
                tcs.SetResult(string.Empty);
                return null;
            }

            Logger.Debug("Got CDN auth token for {Host}, expires {Expiry}", host,
                DateTimeOffset.FromUnixTimeSeconds(response.Body.expiration_time));
            // Update the entry with the real expiry
            _cdnAuthTokens[key] = (tcs, response.Body.expiration_time);
            tcs.SetResult(response.Body.token);
            return response.Body.token;
        }
        catch (Exception ex)
        {
            _cdnAuthTokens.TryRemove(key, out _);
            tcs.TrySetException(ex);
            throw;
        }
    }

    public async Task DownloadWorkshopFileAsync(ulong publishedFileId, string outputBaseDirectory,
                                                CancellationToken cancellationToken = default)
    {
        if (_username.IsNullOrEmpty())
        {
            throw new InvalidOperationException("Must call LoginAsync before downloading workshop file.");
        }

        try
        {
            Logger.Information("Downloading workshop file {PublishedFileId}", publishedFileId);
            var service = _steamUnifiedMessages.CreateService<PublishedFile>();
            var detailsResponse = await service.GetDetails(new CPublishedFile_GetDetails_Request
            {
                publishedfileids = { publishedFileId }, includeadditionalpreviews = true, includechildren = true,
                short_description = true, strip_description_bbcode = false,
            });
            var details = detailsResponse.Body.publishedfiledetails.Single();
            Logger.Information(
                "Workshop item: {Title}, App: {ConsumerAppId}, File: {FileName}, Size: {Size}, HFile: {HFile}",
                details.title, details.consumer_appid, details.filename, details.file_size, details.hcontent_file);
            var outputPath = Path.Combine(outputBaseDirectory, publishedFileId.ToString());
            if (!string.IsNullOrEmpty(details.file_url))
            {
                Logger.Information("File has direct URL, downloading directly...");
                await DownloadDirectAsync(details.file_url, outputPath, cancellationToken);
                return;
            }

            var target =
                await ResolveDepotTargetAsync(details.consumer_appid, details.hcontent_file, cancellationToken);
            await DownloadDepotTargetAsync(target, outputPath, cancellationToken);
        }
        catch (AsyncJobFailedException)
        {
            Logger.Warning("Steam session is no longer valid, reconnecting...");
            await ReconnectAsync(cancellationToken);
            throw;
        }
    }

    private static async Task DownloadDirectAsync(string url, string outputPath,
                                                  CancellationToken cancellationToken = default)
    {
        await using var input = await Http.GetStreamAsync(url, cancellationToken);
        await using var output = File.Create(outputPath);
        await input.CopyToAsync(output, cancellationToken);
    }

    private async Task<DepotDownloadTarget> ResolveDepotTargetAsync(uint appId, ulong hcontentFile,
                                                                    CancellationToken cancellationToken = default)
    {
        Logger.Debug("Resolving depot target. app={AppId}, hcontent={HContent}", appId, hcontentFile);
        if (hcontentFile == 0)
        {
            throw new InvalidOperationException("Published file has hcontent_file = 0.");
        }

        var depotKeyResult = await _steamApps.GetDepotDecryptionKey(appId, appId).ToTask().WaitAsync(cancellationToken);
        if (depotKeyResult.Result != EResult.OK)
        {
            throw new InvalidOperationException($"Failed to get depot key: {depotKeyResult.Result}");
        }

        var directory = _steamClient.Configuration.GetAsyncWebAPIInterface("IContentServerDirectoryService");
        var response = await directory.CallAsync(HttpMethod.Get, "GetServersForSteamPipe", 1,
            new Dictionary<string, object?> { ["cell_id"] = _steamClient.CellID ?? 0, ["max_servers"] = 100, });
        var servers = response["servers"].Children.Where(s => s["type"].AsString() is "SteamCache")
                                         .Where(s => s["https_support"].AsString() == "mandatory")
                                         .Where(s =>
                                              (s["vhost"].AsString() ?? s["host"].AsString()!).EndsWith(
                                                  "steamcontent.com")).Select(s =>
                                              (SteamKit2.CDN.Server)new DnsEndPoint(
                                                  s["vhost"].AsString() ?? s["host"].AsString()!, 443)).ToList();
        if (servers.Count == 0)
        {
            Logger.Warning("No depot servers found.");
            await Task.Delay(5000, cancellationToken);
            throw new InvalidOperationException("No depot servers found.");
        }

        return new DepotDownloadTarget(AppId: appId, ManifestId: hcontentFile, DepotKey: depotKeyResult.DepotKey,
            ServerPool: new CdnServerPool(servers));
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        _reconnectTcs = new TaskCompletionSource();
        _steamClient.Disconnect();
        // OnDisconnected will fire, then OnConnected, then OnLoggedOn
        // which will complete _reconnectTcs
        await _reconnectTcs.Task.WaitAsync(cancellationToken);
    }

    private async Task DownloadDepotTargetAsync(DepotDownloadTarget target, string outputPath,
                                                CancellationToken cancellationToken = default)
    {
        Logger.Information("Downloading depot target");
        Logger.Debug("App: {AppId}, Manifest: {ManifestId}, Output: {OutputPath}", target.AppId, target.ManifestId,
            outputPath);
        var contentService = _steamUnifiedMessages.CreateService<ContentServerDirectory>();
        var manifestCodeResponse = await contentService.GetManifestRequestCode(
            new CContentServerDirectory_GetManifestRequestCode_Request
            {
                app_id = target.AppId, depot_id = target.AppId, manifest_id = target.ManifestId, app_branch = "public",
            });
        Logger.Debug("Manifest request code: {Code}", manifestCodeResponse.Body.manifest_request_code);

        var manifestServer = await target.ServerPool.RentAsync(TimeSpan.FromSeconds(30), cancellationToken);
        if (manifestServer is null)
        {
            throw new InvalidOperationException("No CDN servers available to download manifest.");
        }

        DepotManifest manifest;
        try
        {
            manifest = await _cdnClient.DownloadManifestAsync(depotId: target.AppId, manifestId: target.ManifestId,
                manifestRequestCode: manifestCodeResponse.Body.manifest_request_code, server: manifestServer,
                depotKey: target.DepotKey);
        }
        catch
        {
            target.ServerPool.MarkBroken(manifestServer);
            throw;
        }

        target.ServerPool.Return(manifestServer);
        Logger.Information("Manifest resolved. Found {Count} files.", manifest.Files!.Count);
        Directory.CreateDirectory(outputPath);
        var chunkQueue = new ConcurrentQueue<ChunkDownloadRequest>();
        var fileStreams = new Dictionary<string, (FileStream Stream, SemaphoreSlim Lock)>();
        try
        {
            foreach (var file in manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var filePath = Path.Combine(outputPath, file.FileName.Replace('/', Path.DirectorySeparatorChar));
                if (file.Flags.HasFlag(EDepotFileFlag.Directory))
                {
                    Directory.CreateDirectory(filePath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                Logger.Information("Pre-allocating {FileName} ({Size} bytes)", file.FileName, file.TotalSize);
                var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920,
                    useAsync: true);
                fs.SetLength((long)file.TotalSize);
                var fileLock = new SemaphoreSlim(1, 1);
                fileStreams[file.FileName] = (fs, fileLock);
                foreach (var chunk in file.Chunks)
                {
                    chunkQueue.Enqueue(new ChunkDownloadRequest(target, chunk, fs, fileLock));
                }
            }

            Logger.Information("Downloading {Count} chunks across {Files} files", chunkQueue.Count, fileStreams.Count);
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 10,
                CancellationToken = cancellationToken,
            };
            await Parallel.ForEachAsync(chunkQueue.Select((request, index) => (index, request)), parallelOptions,
                async (data, ct) =>
                {
                    await DownloadChunkWithRetryAsync(data.request, ct);
                    Logger.Debug("Finished downloading chunk {Chunk}", data.index);
                });
        }
        catch (IOException e) when (e.Message.Contains("There is not enough space on the disk"))
        {
            Logger.Error(e, "Disk full while downloading depot target");
            throw new NotEnoughDiskSpaceException(e);
        }
        finally
        {
            foreach (var (fs, fileLock) in fileStreams.Values)
            {
                await fs.DisposeAsync();
                fileLock.Dispose();
            }
        }

        Logger.Information("Download complete");
    }

    private async Task DownloadChunkWithRetryAsync(ChunkDownloadRequest request,
                                                   CancellationToken cancellationToken = default)
    {
        const int maxRetries = 5;
        var rentTimeout = TimeSpan.FromSeconds(30);
        var delay = TimeSpan.FromSeconds(30);
        var pool = request.Target.ServerPool;
        var destination = ArrayPool<byte>.Shared.Rent((int)request.Chunk.UncompressedLength);
        try
        {
            for (var attempt = 0; attempt < maxRetries; attempt++)
            {
                var server = await pool.RentAsync(rentTimeout, cancellationToken);
                if (server is null)
                {
                    throw new InvalidOperationException(
                        $"No CDN servers available for chunk at offset {request.Chunk.Offset}.");
                }

                try
                {
                    string? cdnToken = null;
                    if (_cdnAuthTokens.TryGetValue((request.Target.AppId, server.Host!), out var entry))
                    {
                        cdnToken = await entry.Tcs.Task;
                    }

                    await _cdnClient.DownloadDepotChunkAsync(request.Target.AppId, request.Chunk, server, destination,
                        request.Target.DepotKey, cdnAuthToken: cdnToken);
                    pool.Return(server);
                    await request.FileLock.WaitAsync(cancellationToken);
                    try
                    {
                        request.FileStream.Seek((long)request.Chunk.Offset, SeekOrigin.Begin);
                        await request.FileStream.WriteAsync(
                            destination.AsMemory(0, (int)request.Chunk.UncompressedLength), cancellationToken);
                    }
                    finally
                    {
                        request.FileLock.Release();
                    }

                    return;
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    pool.MarkBroken(server);
                    if (attempt == maxRetries - 1)
                    {
                        throw;
                    }

                    Logger.Warning(
                        "CDN request timed out on {Host}, server marked broken, retrying in {Delay}s (attempt {Attempt}/{Max})",
                        server.Host, delay.TotalSeconds, attempt + 1, maxRetries);
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                }
                catch (SteamKitWebRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    var key = (request.Target.AppId, server.Host!);
                    if (!_cdnAuthTokens.TryGetValue(key, out var existingEntry) || existingEntry.Tcs.Task.IsCompleted)
                    {
                        _cdnAuthTokens.TryRemove(key, out _);
                        Logger.Warning("Got 403 from {Host}, requesting CDN auth token", server.Host);
                        await GetCdnAuthTokenAsync(request.Target.AppId, server.Host!,
                            cancellationToken: cancellationToken);
                        pool.Return(server);
                        attempt--;
                        continue;
                    }

                    await existingEntry.Tcs.Task;
                    pool.Return(server);
                    attempt--;
                }
                catch (SteamKitWebRequestException ex) when (ex.StatusCode is HttpStatusCode.ServiceUnavailable
                                                                 or HttpStatusCode.TooManyRequests
                                                                 or HttpStatusCode.InternalServerError)
                {
                    pool.MarkTransientFailure(server, delay);
                    if (attempt == maxRetries - 1)
                    {
                        throw;
                    }

                    Logger.Warning(
                        "CDN request failed with {Status} on {Host}, cooling down for {Delay}s, retrying (attempt {Attempt}/{Max})",
                        ex.StatusCode, server.Host, delay.TotalSeconds, attempt + 1, maxRetries);
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                }
                catch
                {
                    pool.Return(server);
                    throw;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(destination);
        }
    }

    public async Task<IReadOnlyList<PublishedFileDetails>> GetUserWorkshopItemsAsync(
        ulong steamId, uint appId, CancellationToken cancellationToken = default)
    {
        Logger.Information("Getting workshop items for user {SteamId}, app {AppId}", steamId, appId);
        var service = _steamUnifiedMessages.CreateService<PublishedFile>();
        const uint pageSize = 100;
        var results = new List<PublishedFileDetails>();
        uint page = 1;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await service.GetUserFiles(new CPublishedFile_GetUserFiles_Request
                { steamid = steamId, appid = appId, page = page, numperpage = pageSize, sortmethod = "lastupdated", });
            var files = response.Body.publishedfiledetails;
            if (files.Count == 0)
            {
                break;
            }

            results.AddRange(files);
            Logger.Information("Fetched page {Page}, got {Count} items, total so far {Total}", page, files.Count,
                results.Count);
            if (results.Count >= (int)response.Body.total)
            {
                break;
            }

            page++;
        }

        Logger.Information("Found {Total} workshop items for user {SteamId}, app {AppId}", results.Count, steamId,
            appId);
        return results;
    }

    public async Task<string> GetPersonaNameAsync(ulong steamId, CancellationToken cancellationToken = default)
    {
        Logger.Information("Getting persona name for {SteamId}", steamId);
        var service = _steamUnifiedMessages.CreateService<Player>();
        var response = await service.GetPlayerLinkDetails(new CPlayer_GetPlayerLinkDetails_Request
            { steamids = { steamId } });
        var name = response.Body.accounts.Single().public_data.persona_name;
        if (string.IsNullOrEmpty(name))
        {
            Logger.Warning("Persona name was empty for SteamID {SteamId}", steamId);
            return steamId.ToString();
        }

        Logger.Information("Resolved persona name: {Name}", name);
        return name;
    }

    public static async Task<ulong> ResolveVanityUrlAsync(string vanityUrl, CancellationToken cancellationToken = default)
    {
        Logger.Information("Resolving vanity URL {VanityUrl}", vanityUrl);
        var response = await Http.GetAsync($"https://steamcommunity.com/id/{Uri.EscapeDataString(vanityUrl)}/?xml=1",
            cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = System.Xml.Linq.XDocument.Parse(content);
        var steamIdElement = doc.Root?.Element("steamID64");
        if (steamIdElement == null)
        {
            throw new InvalidOperationException($"Failed to resolve vanity URL '{vanityUrl}': profile not found");
        }

        var steamId = ulong.Parse(steamIdElement.Value);
        Logger.Information("Resolved {VanityUrl} -> {SteamId}", vanityUrl, steamId);
        return steamId;
    }

    private async void OnConnected(SteamClient.ConnectedCallback callback)
    {
        try
        {
            Logger.Information("Connected to Steam! Logging in {UserName}", _username);
            var shouldRememberPassword = true;
            var config = SteamConfig.Instance;
            string accountName;
            string refreshToken;
            if (string.IsNullOrWhiteSpace(config.RefreshToken) || string.IsNullOrWhiteSpace(config.GuardData))
            {
                var authSession = await _steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(
                    new AuthSessionDetails
                    {
                        Username = _username, Password = _password, IsPersistentSession = shouldRememberPassword,
                        GuardData = _previouslyStoredGuardData, Authenticator = new UserConsoleAuthenticator()
                    });
                Logger.Information("Waiting for logon result...");
                var pollResponse = await authSession.PollingWaitForResultAsync();
                if (pollResponse.NewGuardData is not null)
                {
                    _previouslyStoredGuardData = pollResponse.NewGuardData;
                }

                accountName = pollResponse.AccountName;
                refreshToken = pollResponse.RefreshToken;
                config.RefreshToken = refreshToken;
                config.GuardData = _previouslyStoredGuardData ?? string.Empty;
                SteamConfig.Save(config);
            }
            else
            {
                accountName = _username;
                refreshToken = config.RefreshToken;
            }

            _steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = accountName, AccessToken = refreshToken, ShouldRememberPassword = shouldRememberPassword,
            });
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error while connecting to Steam");
            _loginTcs.TrySetException(e);
        }
    }

    private static void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        Logger.Information("Logged off of Steam: {Result}", callback.Result);
    }
}