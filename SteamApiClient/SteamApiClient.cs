using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using Serilog;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using Common.ExtensionMethods;

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
            //Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => _steamClient.Connect());
            return;
        }

        if (callback.Result != EResult.OK)
        {
            Logger.Error("Unable to logon to Steam: {Result} / {ExtendedResult}",
                callback.Result, callback.ExtendedResult);
            _loginTcs.SetException(new InvalidOperationException(
                $"Steam login failed: {callback.Result}"));
            return;
        }

        Logger.Information("Successfully logged on!");
        _loginTcs.SetResult();
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        Logger.Information("Disconnected from Steam");

        if (!_loginTcs.Task.IsCompleted)
        {
            _steamClient.Connect();
        }
    }

    private readonly
        ConcurrentDictionary<(uint DepotId, string Host), (TaskCompletionSource<string> Tcs, long ExpiryUnix)>
        _cdnAuthTokens = new();

    private async Task<string?> GetCdnAuthTokenAsync(uint depotId, string host)
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
            // Lost the race to another thread — use theirs
            return await _cdnAuthTokens[key].Tcs.Task;
        }

        try
        {
            Logger.Debug("Requesting CDN auth token for depot {DepotId} host {Host}", depotId, host);

            var contentService = _steamUnifiedMessages.CreateService<ContentServerDirectory>();

            var response = await contentService.GetCDNAuthToken(
                new CContentServerDirectory_GetCDNAuthToken_Request
                {
                    depot_id = depotId,
                    host_name = host,
                    app_id = depotId,
                });

            if (response.Result != EResult.OK)
            {
                Logger.Warning("CDN auth token request failed for {Host}: {Result}", host, response.Result);
                _cdnAuthTokens.TryRemove(key, out _);
                tcs.SetResult(string.Empty);
                return null;
            }

            Logger.Debug("Got CDN auth token for {Host}, expires {Expiry}",
                host, DateTimeOffset.FromUnixTimeSeconds(response.Body.expiration_time));

            // Update the entry with the real expiry now that we have it
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

        Logger.Information("Downloading workshop file {PublishedFileId}", publishedFileId);

        var service = _steamUnifiedMessages.CreateService<PublishedFile>();

        var detailsResponse = await service.GetDetails(
            new CPublishedFile_GetDetails_Request
            {
                publishedfileids = { publishedFileId },
                includeadditionalpreviews = true,
                includechildren = true,
                short_description = true,
                strip_description_bbcode = false,
            });

        var details = detailsResponse.Body.publishedfiledetails.Single();

        Logger.Information(
            "Workshop item: {Title}, App: {ConsumerAppId}, File: {FileName}, Size: {Size}, HFile: {HFile}",
            details.title,
            details.consumer_appid,
            details.filename,
            details.file_size,
            details.hcontent_file);

        var outputPath = Path.Combine(outputBaseDirectory, publishedFileId.ToString());

        if (!string.IsNullOrEmpty(details.file_url))
        {
            Logger.Information("File has direct URL, downloading directly...");
            await DownloadDirectAsync(details.file_url, outputPath, cancellationToken);
            return;
        }

        var target = await ResolveDepotTargetAsync(details.consumer_appid, details.hcontent_file, cancellationToken);
        await DownloadDepotTargetAsync(target, outputPath, cancellationToken);
    }

    private static async Task DownloadDirectAsync(
        string url,
        string outputPath,
        CancellationToken cancellationToken)
    {
        await using var input = await Http.GetStreamAsync(url, cancellationToken);
        await using var output = File.Create(outputPath);

        await input.CopyToAsync(output, cancellationToken);
    }

    private sealed record DepotDownloadTarget(
        uint AppId,
        ulong ManifestId,
        byte[] DepotKey,
        CdnServerPool ServerPool
    );

    private sealed class CdnServerPool
    {
        private readonly ConcurrentBag<SteamKit2.CDN.Server> _available;
        private readonly ConcurrentBag<SteamKit2.CDN.Server> _broken;
        private readonly ILogger _logger = Log.ForContext<CdnServerPool>();

        public CdnServerPool(IReadOnlyList<SteamKit2.CDN.Server> servers)
        {
            _available = new ConcurrentBag<SteamKit2.CDN.Server>(servers);
            _broken = new ConcurrentBag<SteamKit2.CDN.Server>();
        }

        public bool TryRent(out SteamKit2.CDN.Server server)
        {
            if (_available.TryTake(out server!))
            {
                return true;
            }

            // All servers exhausted — try recycling broken ones as last resort
            if (_broken.TryTake(out server!))
            {
                _logger.Warning("All healthy servers exhausted, retrying broken server {Host}", server.Host);
                return true;
            }

            return false;
        }

        public void Return(SteamKit2.CDN.Server server)
        {
            _available.Add(server);
        }

        public void MarkBroken(SteamKit2.CDN.Server server)
        {
            _logger.Warning("Marking server {Host} as broken", server.Host);
            _broken.Add(server);
        }

        public int AvailableCount => _available.Count;
        public int BrokenCount => _broken.Count;
    }

    private async Task<DepotDownloadTarget> ResolveDepotTargetAsync(
        uint appId,
        ulong hcontentFile,
        CancellationToken cancellationToken)
    {
        Logger.Debug(
            "Resolving depot target. app={AppId}, hcontent={HContent}",
            appId, hcontentFile);

        if (hcontentFile == 0)
        {
            throw new InvalidOperationException("Published file has hcontent_file = 0.");
        }

        var depotKeyResult = await _steamApps
                                  .GetDepotDecryptionKey(appId, appId)
                                  .ToTask()
                                  .WaitAsync(cancellationToken);

        if (depotKeyResult.Result != EResult.OK)
        {
            throw new InvalidOperationException($"Failed to get depot key: {depotKeyResult.Result}");
        }

        var directory = _steamClient.Configuration
                                    .GetAsyncWebAPIInterface("IContentServerDirectoryService");

        var response = await directory.CallAsync(HttpMethod.Get, "GetServersForSteamPipe", 1,
            new Dictionary<string, object?>
            {
                ["cell_id"] = _steamClient.CellID ?? 0,
                ["max_servers"] = 100,
            });

        var servers = response["servers"].Children
                                         .Where(s => s["type"].AsString() is "SteamCache" or "CDN")
                                         .Where(s => s["https_support"].AsString() == "mandatory")
                                         .Select(s => (SteamKit2.CDN.Server)new DnsEndPoint(
                                              s["vhost"].AsString() ?? s["host"].AsString()!, 443))
                                         .Where(s => s.Host is not null)
                                         .ToList();

        return new DepotDownloadTarget(
            AppId: appId,
            ManifestId: hcontentFile,
            DepotKey: depotKeyResult.DepotKey,
            ServerPool: new CdnServerPool(servers));
    }

    private sealed record ChunkDownloadRequest(
        DepotDownloadTarget Target,
        DepotManifest.ChunkData Chunk,
        FileStream FileStream,
        SemaphoreSlim FileLock
    );

    private async Task DownloadDepotTargetAsync(
        DepotDownloadTarget target,
        string outputPath,
        CancellationToken cancellationToken)
    {
        Logger.Information("Downloading depot target");
        Logger.Debug(
            "App: {AppId}, Manifest: {ManifestId}, Output: {OutputPath}",
            target.AppId, target.ManifestId, outputPath);

        var contentService = _steamUnifiedMessages.CreateService<ContentServerDirectory>();

        var manifestCodeResponse = await contentService.GetManifestRequestCode(
            new CContentServerDirectory_GetManifestRequestCode_Request
            {
                app_id = target.AppId,
                depot_id = target.AppId,
                manifest_id = target.ManifestId,
                app_branch = "public",
            });

        Logger.Debug("Manifest request code: {Code}", manifestCodeResponse.Body.manifest_request_code);

        if (!target.ServerPool.TryRent(out var manifestServer))
        {
            throw new InvalidOperationException("No CDN servers available to download manifest.");
        }

        DepotManifest manifest;
        try
        {
            manifest = await _cdnClient.DownloadManifestAsync(
                depotId: target.AppId,
                manifestId: target.ManifestId,
                manifestRequestCode: manifestCodeResponse.Body.manifest_request_code,
                server: manifestServer,
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

        // Phase 1: pre-allocate all files and enqueue chunks
        var chunkQueue = new ConcurrentQueue<ChunkDownloadRequest>();
        var fileStreams = new Dictionary<string, (FileStream Stream, SemaphoreSlim Lock)>();

        try
        {
            foreach (var file in manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = Path.Combine(outputPath,
                    file.FileName.Replace('/', Path.DirectorySeparatorChar));

                if (file.Flags.HasFlag(EDepotFileFlag.Directory))
                {
                    Directory.CreateDirectory(filePath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                Logger.Information("Pre-allocating {FileName} ({Size} bytes)", file.FileName, file.TotalSize);

                var fs = new FileStream(
                    filePath, FileMode.Create, FileAccess.Write,
                    FileShare.None, bufferSize: 81920, useAsync: true);

                fs.SetLength((long)file.TotalSize);

                var fileLock = new SemaphoreSlim(1, 1);
                fileStreams[file.FileName] = (fs, fileLock);

                foreach (var chunk in file.Chunks)
                {
                    chunkQueue.Enqueue(new ChunkDownloadRequest(target, chunk, fs, fileLock));
                }
            }

            // Phase 2: download all chunks in parallel
            Logger.Information("Downloading {Count} chunks across {Files} files",
                chunkQueue.Count, fileStreams.Count);

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 10,
                CancellationToken = cancellationToken,
            };

            await Parallel.ForEachAsync(chunkQueue, parallelOptions,
                async (request, ct) => { await DownloadChunkWithRetryAsync(request, ct); });
        }
        finally
        {
            // Ensure all streams are closed even if download fails partway through
            foreach (var (fs, fileLock) in fileStreams.Values)
            {
                await fs.DisposeAsync();
                fileLock.Dispose();
            }
        }

        Logger.Information("Download complete");
    }

    private async Task DownloadChunkWithRetryAsync(
        ChunkDownloadRequest request,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 5;
        var delay = TimeSpan.FromSeconds(30);
        var pool = request.Target.ServerPool;

        var destination = ArrayPool<byte>.Shared.Rent((int)request.Chunk.UncompressedLength);
        try
        {
            for (var attempt = 0; attempt < maxRetries; attempt++)
            {
                if (!pool.TryRent(out var server))
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

                    await _cdnClient.DownloadDepotChunkAsync(
                        request.Target.AppId,
                        request.Chunk,
                        server,
                        destination,
                        request.Target.DepotKey,
                        cdnAuthToken: cdnToken);

                    pool.Return(server);

                    await request.FileLock.WaitAsync(cancellationToken);
                    try
                    {
                        request.FileStream.Seek((long)request.Chunk.Offset, SeekOrigin.Begin);
                        await request.FileStream.WriteAsync(
                            destination.AsMemory(0, (int)request.Chunk.UncompressedLength),
                            cancellationToken);
                    }
                    finally
                    {
                        request.FileLock.Release();
                    }

                    return;
                }
                catch (SteamKitWebRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    var key = (request.Target.AppId, server.Host!);

                    if (!_cdnAuthTokens.TryGetValue(key, out var existingEntry) ||
                        existingEntry.Tcs.Task.IsCompleted)
                    {
                        _cdnAuthTokens.TryRemove(key, out _);

                        Logger.Warning("Got 403 from {Host}, requesting CDN auth token", server.Host);
                        await GetCdnAuthTokenAsync(request.Target.AppId, server.Host!);

                        // Return server to pool — it may work now with a token
                        pool.Return(server);
                        attempt--;
                        continue;
                    }

                    // Token is in-flight, await it and return server to try again
                    await existingEntry.Tcs.Task;
                    pool.Return(server);
                }
                catch (SteamKitWebRequestException ex) when (ex.StatusCode is
                                                                 HttpStatusCode.ServiceUnavailable or
                                                                 HttpStatusCode.TooManyRequests or
                                                                 HttpStatusCode.InternalServerError)
                {
                    pool.MarkBroken(server);

                    if (attempt == maxRetries - 1)
                    {
                        throw;
                    }

                    Logger.Warning(
                        "CDN request failed with {Status} on {Host}, server marked broken, retrying in {Delay}s (attempt {Attempt}/{Max})",
                        ex.StatusCode, server.Host, delay.TotalSeconds, attempt + 1, maxRetries);

                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                }
                catch
                {
                    // For any unexpected exception, return the server rather than
                    // permanently evicting it — we don't know if it's the server's fault
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
        ulong steamId,
        uint appId,
        CancellationToken cancellationToken = default)
    {
        Logger.Information(
            "Getting workshop items for user {SteamId}, app {AppId}",
            steamId, appId);

        var service = _steamUnifiedMessages.CreateService<PublishedFile>();

        const uint pageSize = 100;
        var results = new List<PublishedFileDetails>();
        uint page = 1;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await service.GetUserFiles(
                new CPublishedFile_GetUserFiles_Request
                {
                    steamid = steamId,
                    appid = appId,
                    page = page,
                    numperpage = pageSize,
                    sortmethod = "lastupdated",
                });

            var files = response.Body.publishedfiledetails;

            if (files.Count == 0)
            {
                break;
            }

            results.AddRange(files);

            Logger.Information(
                "Fetched page {Page}, got {Count} items, total so far {Total}",
                page, files.Count, results.Count);

            if (results.Count >= (int)response.Body.total)
            {
                break;
            }

            page++;
        }

        Logger.Information(
            "Found {Total} workshop items for user {SteamId}, app {AppId}",
            results.Count, steamId, appId);

        return results;
    }

    public async Task<string> GetPersonaNameAsync(
        ulong steamId,
        CancellationToken cancellationToken = default)
    {
        Logger.Information("Getting persona name for {SteamId}", steamId);

        var service = _steamUnifiedMessages.CreateService<Player>();

        var response = await service.GetPlayerLinkDetails(
            new CPlayer_GetPlayerLinkDetails_Request
            {
                steamids = { steamId }
            });

        var name = response.Body.accounts.Single().public_data.persona_name;

        if (string.IsNullOrEmpty(name))
        {
            Logger.Warning("Persona name was empty for SteamID {SteamId}", steamId);
            return steamId.ToString();
        }

        Logger.Information("Resolved persona name: {Name}", name);

        return name;
    }

    public async Task<ulong> ResolveVanityUrlAsync(
        string vanityUrl,
        CancellationToken cancellationToken = default)
    {
        Logger.Information("Resolving vanity URL {VanityUrl}", vanityUrl);

        var response = await Http.GetAsync(
            $"https://steamcommunity.com/id/{Uri.EscapeDataString(vanityUrl)}/?xml=1",
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        var doc = System.Xml.Linq.XDocument.Parse(content);
        var steamIdElement = doc.Root?.Element("steamID64");

        if (steamIdElement == null)
        {
            throw new InvalidOperationException(
                $"Failed to resolve vanity URL '{vanityUrl}': profile not found");
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
                        Username = _username,
                        Password = _password,
                        IsPersistentSession = shouldRememberPassword,
                        GuardData = _previouslyStoredGuardData,
                        Authenticator = new UserConsoleAuthenticator()
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
                Username = accountName,
                AccessToken = refreshToken,
                ShouldRememberPassword = shouldRememberPassword,
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