using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Win32.SafeHandles;
using Serilog;
using SteamKit2;
using SteamKit2.Internal;
using CdnServer = SteamKit2.CDN.Server;

namespace SteamApiClient;

internal sealed class DepotDownloader : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<DepotDownloader>();

    private readonly SteamSession _session;
    private readonly SteamApiClientOptions _options;
    private readonly SteamKit2.CDN.Client _cdnClient;
    private readonly CdnAuthTokenCache _cdnAuthTokens;
    private readonly ConcurrentDictionary<(uint AppId, uint DepotId), Lazy<Task<byte[]>>> _depotKeys = new();
    private readonly Lock _serverListLock = new();
    private (Task<IReadOnlyList<CdnServer>> Servers, DateTimeOffset FetchedAt)? _serverList;

    public DepotDownloader(SteamSession session, SteamApiClientOptions options)
    {
        _session = session;
        _options = options;
        _cdnClient = new SteamKit2.CDN.Client(session.Client);
        _cdnAuthTokens = new CdnAuthTokenCache(session.UnifiedMessages);
    }

    public async Task<DepotDownloadTarget> ResolveTargetAsync(uint appId, uint depotId, ulong manifestId,
                                                              CancellationToken cancellationToken = default)
    {
        Logger.Debug("Resolving depot target. app={AppId}, depot={DepotId}, manifest={ManifestId}", appId, depotId,
            manifestId);
        if (manifestId == 0)
        {
            throw new SteamApiException("Published file has no content manifest (hcontent_file = 0).");
        }

        var depotKey = await GetDepotKeyAsync(appId, depotId, cancellationToken);
        var servers = await GetCdnServersAsync(cancellationToken);
        return new DepotDownloadTarget(appId, depotId, manifestId, depotKey, new CdnServerPool(servers));
    }

    private async Task<byte[]> GetDepotKeyAsync(uint appId, uint depotId, CancellationToken cancellationToken)
    {
        var key = (appId, depotId);
        var entry = _depotKeys.GetOrAdd(key, _ => new Lazy<Task<byte[]>>(async () =>
        {
            var result = await _session.Apps.GetDepotDecryptionKey(depotId, appId).ToTask();
            if (result.Result != EResult.OK)
            {
                throw new SteamApiException($"Failed to get depot key: {result.Result}", result.Result);
            }

            return result.DepotKey;
        }));
        try
        {
            return await entry.Value.WaitAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Don't cache failures
            _depotKeys.TryRemove(new KeyValuePair<(uint, uint), Lazy<Task<byte[]>>>(key, entry));
            throw;
        }
    }

    private async Task<IReadOnlyList<CdnServer>> GetCdnServersAsync(CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<CdnServer>> serversTask;
        lock (_serverListLock)
        {
            if (_serverList is not { } cached || cached.Servers.IsFaulted ||
                DateTimeOffset.UtcNow - cached.FetchedAt > _options.CdnServerListLifetime)
            {
                _serverList = (FetchCdnServersAsync(), DateTimeOffset.UtcNow);
            }

            serversTask = _serverList.Value.Servers;
        }

        return await serversTask.WaitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CdnServer>> FetchCdnServersAsync()
    {
        var directory = _session.Client.Configuration.GetAsyncWebAPIInterface("IContentServerDirectoryService");
        var response = await directory.CallAsync(HttpMethod.Get, "GetServersForSteamPipe", 1,
            new Dictionary<string, object?>
                { ["cell_id"] = _session.Client.CellID ?? 0, ["max_servers"] = _options.MaxCdnServers });
        var servers = response["servers"].Children
                                         .Where(s => s["type"].AsString() == "SteamCache" &&
                                                     s["https_support"].AsString() == "mandatory")
                                         .Select(s => s["vhost"].AsString() ?? s["host"].AsString()!)
                                         .Where(host => host.EndsWith("steamcontent.com"))
                                         .Select(host => (CdnServer)new DnsEndPoint(host, 443))
                                         .ToList();
        if (servers.Count == 0)
        {
            throw new SteamApiException("No depot servers found.");
        }

        Logger.Debug("Found {Count} CDN servers", servers.Count);
        return servers;
    }

    public async Task DownloadAsync(DepotDownloadTarget target, string outputPath,
                                    CancellationToken cancellationToken = default)
    {
        Logger.Information("Downloading depot target");
        Logger.Debug("App: {AppId}, Depot: {DepotId}, Manifest: {ManifestId}, Output: {OutputPath}", target.AppId,
            target.DepotId, target.ManifestId, outputPath);
        var manifest = await DownloadManifestAsync(target, cancellationToken);
        var files = manifest.Files ?? [];
        Logger.Information("Manifest resolved. Found {Count} files.", files.Count);

        var outputRoot = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(outputRoot);
        var chunks = new List<ChunkDownloadRequest>();
        var handles = new List<SafeFileHandle>();
        try
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var filePath = ResolveOutputPath(outputRoot, file.FileName);
                if (file.Flags.HasFlag(EDepotFileFlag.Directory))
                {
                    Directory.CreateDirectory(filePath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                Logger.Information("Pre-allocating {FileName} ({Size} bytes)", file.FileName, file.TotalSize);
                var handle = File.OpenHandle(filePath, FileMode.Create, FileAccess.Write, FileShare.None,
                    FileOptions.Asynchronous);
                handles.Add(handle);
                RandomAccess.SetLength(handle, (long)file.TotalSize);
                chunks.AddRange(file.Chunks.Select(chunk => new ChunkDownloadRequest(chunk, handle)));
            }

            Logger.Information("Downloading {Count} chunks across {Files} files", chunks.Count, handles.Count);
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxConcurrentChunkDownloads,
                CancellationToken = cancellationToken,
            };
            await Parallel.ForEachAsync(chunks, parallelOptions,
                async (request, ct) => await DownloadChunkAsync(target, request, ct));
        }
        catch (IOException e) when (IsDiskFull(e))
        {
            Logger.Error(e, "Disk full while downloading depot target");
            throw;
        }
        finally
        {
            foreach (var handle in handles)
            {
                handle.Dispose();
            }
        }

        Logger.Information("Download complete");
    }

    /// <summary>Manifest file names come from user-uploaded content; refuse anything that escapes the output root.</summary>
    private static string ResolveOutputPath(string outputRoot, string manifestFileName)
    {
        var filePath = Path.GetFullPath(Path.Combine(outputRoot,
            manifestFileName.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!filePath.StartsWith(Path.TrimEndingDirectorySeparator(outputRoot) + Path.DirectorySeparatorChar,
                comparison))
        {
            throw new SteamApiException($"Manifest file '{manifestFileName}' resolves outside the output directory.");
        }

        return filePath;
    }

    private static bool IsDiskFull(IOException e)
    {
        const int errorHandleDiskFull = 0x27;
        const int errorDiskFull = 0x70;
        return (e.HResult & 0xFFFF) is errorHandleDiskFull or errorDiskFull;
    }

    private async Task<DepotManifest> DownloadManifestAsync(DepotDownloadTarget target,
                                                            CancellationToken cancellationToken)
    {
        var contentService = _session.UnifiedMessages.CreateService<ContentServerDirectory>();
        var codeResponse = await contentService.GetManifestRequestCode(
            new CContentServerDirectory_GetManifestRequestCode_Request
            {
                app_id = target.AppId, depot_id = target.DepotId, manifest_id = target.ManifestId,
                app_branch = "public",
            }).ToTask().WaitAsync(cancellationToken);
        if (codeResponse.Result != EResult.OK)
        {
            throw new SteamApiException($"Failed to get manifest request code: {codeResponse.Result}",
                codeResponse.Result);
        }

        var requestCode = codeResponse.Body.manifest_request_code;
        Logger.Debug("Manifest request code: {Code}", requestCode);
        return await ExecuteWithRetryAsync(target, "manifest",
            (server, token) => _cdnClient.DownloadManifestAsync(target.DepotId, target.ManifestId, requestCode,
                server, target.DepotKey, cdnAuthToken: token), cancellationToken);
    }

    private async Task DownloadChunkAsync(DepotDownloadTarget target, ChunkDownloadRequest request,
                                          CancellationToken cancellationToken)
    {
        var chunk = request.Chunk;
        var buffer = ArrayPool<byte>.Shared.Rent((int)chunk.UncompressedLength);
        try
        {
            var written = await ExecuteWithRetryAsync(target, $"chunk at offset {chunk.Offset}",
                (server, token) => _cdnClient.DownloadDepotChunkAsync(target.DepotId, chunk, server, buffer,
                    target.DepotKey, cdnAuthToken: token), cancellationToken);
            await RandomAccess.WriteAsync(request.File, buffer.AsMemory(0, written), (long)chunk.Offset,
                cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private enum FailureKind
    {
        Forbidden,
        Transient,
        BrokenServer,
    }

    private static FailureKind? Classify(Exception e) => e switch
    {
        SteamKitWebRequestException { StatusCode: HttpStatusCode.Forbidden } => FailureKind.Forbidden,
        SteamKitWebRequestException
        {
            StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
        } => FailureKind.Transient,
        // Timeouts, connection failures, other HTTP errors, and corrupt/short chunk data: try another server
        SteamKitWebRequestException or TaskCanceledException or HttpRequestException or IOException =>
            FailureKind.BrokenServer,
        _ => null,
    };

    /// <summary>
    /// Runs <paramref name="operation"/> against a server rented from the target's pool, retrying on other servers
    /// with exponential back-off. A 403 triggers a CDN auth token refresh that doesn't count as an attempt (up to
    /// <see cref="SteamApiClientOptions.MaxCdnAuthTokenRefreshes"/> times). The operation is not cancellable, so
    /// it's always allowed to finish; this keeps it from writing into a buffer the caller has already released.
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(DepotDownloadTarget target, string description,
                                                   Func<CdnServer, string?, Task<T>> operation,
                                                   CancellationToken cancellationToken)
    {
        var pool = target.ServerPool;
        var delay = _options.InitialRetryDelay;
        var tokenRefreshes = 0;
        var attempt = 0;
        while (true)
        {
            var server = await pool.RentAsync(_options.ServerRentTimeout, cancellationToken) ??
                         throw new SteamApiException($"No CDN servers available for {description}.");
            var host = server.Host!;
            FailureKind? failure;
            Exception error;
            string? token = null;
            try
            {
                token = await _cdnAuthTokens.GetCachedAsync(target.DepotId, host, cancellationToken);
                var result = await operation(server, token);
                pool.Return(server);
                return result;
            }
            catch (Exception e)
            {
                failure = cancellationToken.IsCancellationRequested ? null : Classify(e);
                error = e;
            }

            if (failure == FailureKind.Forbidden && tokenRefreshes < _options.MaxCdnAuthTokenRefreshes)
            {
                pool.Return(server);
                tokenRefreshes++;
                Logger.Warning("Got 403 from {Host} for {Description}, requesting CDN auth token", host,
                    description);
                await _cdnAuthTokens.RefreshAsync(target.AppId, target.DepotId, host, token, cancellationToken);
                continue;
            }

            attempt++;
            switch (failure)
            {
                case FailureKind.Transient:
                    pool.MarkTransientFailure(server, delay);
                    break;
                case FailureKind.Forbidden or FailureKind.BrokenServer:
                    pool.MarkBroken(server);
                    break;
                default:
                    pool.Return(server);
                    throw error.Rethrow();
            }

            if (attempt >= _options.MaxDownloadAttempts)
            {
                throw error.Rethrow();
            }

            Logger.Warning(error,
                "CDN request for {Description} failed on {Host} ({Failure}), retrying in {Delay}s (attempt {Attempt}/{Max})",
                description, host, failure, delay.TotalSeconds, attempt, _options.MaxDownloadAttempts);
            await Task.Delay(delay, cancellationToken);
            delay *= 2;
        }
    }

    public void Dispose() => _cdnClient.Dispose();
}

file static class ExceptionExtensions
{
    /// <summary>Rethrows preserving the original stack trace. Declared as returning so it can be used with <c>throw</c>.</summary>
    public static Exception Rethrow(this Exception e)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e).Throw();
        return e;
    }
}
