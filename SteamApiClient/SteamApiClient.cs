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
    private readonly string _username;
    private readonly string _password;
    private readonly TaskCompletionSource _loginTcs = new();

    private string? _previouslyStoredGuardData;

    public SteamApiClient(string username, string password)
    {
        _username = username;
        _password = password;
        _steamClient = new SteamClient();
        _manager = new CallbackManager(_steamClient);
        _steamUser = _steamClient.GetHandler<SteamUser>()!;
        _steamApps = _steamClient.GetHandler<SteamApps>()!;
        _steamUnifiedMessages = _steamClient.GetHandler<SteamUnifiedMessages>()!;

        _manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

        _manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
    }

    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        _steamClient.Connect();

        // Pump callbacks only until login completes
        await Task.Run(() =>
        {
            while (!_loginTcs.Task.IsCompleted)
            {
                _manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }, cancellationToken);

        await _loginTcs.Task.WaitAsync(cancellationToken);
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result == EResult.TryAnotherCM)
        {
            Logger.Warning("Steam requested another CM; reconnecting...");
            _steamClient.Disconnect();
            Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => _steamClient.Connect());
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
            _loginTcs.SetException(new InvalidOperationException("Disconnected before login completed."));
        }
    }

    public async Task DownloadWorkshopFileAsync(ulong publishedFileId, string outputPath,
                                                CancellationToken cancellationToken = default)
    {
        Logger.Information("Downloading workshop file {PublishedFileId}", publishedFileId);

        var service = _steamUnifiedMessages.CreateService<PublishedFile>();

        var detailsResponse = await service.GetDetails(
            new CPublishedFile_GetDetails_Request
            {
                publishedfileids =
                {
                    publishedFileId
                },
                includetags = false,
                includeadditionalpreviews = true,
                includechildren = true,
                includevotes = false,
                includekvtags = false,
                short_description = true,
                includeforsaledata = false,
                includemetadata = false,
                language = 0,
                return_playtime_stats = 0,
                //appid = 0,
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
        SteamKit2.CDN.Server Server
    );

    private async Task<DepotDownloadTarget> ResolveDepotTargetAsync(
        uint appId,
        ulong hcontentFile,
        CancellationToken cancellationToken)
    {
        Logger.Information(
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
                ["cell_id"] = 0,
                ["max_servers"] = 20,
            });

        var server = response["servers"].Children
                                        .Where(s => s["type"].AsString() is "SteamCache")
                                        .Where(s => s["https_support"].AsString() == "mandatory")
                                        .Select(s => (SteamKit2.CDN.Server)new System.Net.DnsEndPoint(
                                             s["vhost"].AsString() ?? s["host"].AsString()!, 443))
                                        .First();

        Logger.Information("Selected CDN server: {Host}", server.Host);

        return new DepotDownloadTarget(
            AppId: appId,
            ManifestId: hcontentFile,
            DepotKey: depotKeyResult.DepotKey,
            Server: server);
    }

    private async Task DownloadDepotTargetAsync(
        DepotDownloadTarget target,
        string outputPath,
        CancellationToken cancellationToken)
    {
        Logger.Information(
            "Downloading depot target. app={AppId}, manifest={ManifestId}, output={OutputPath}",
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

        Logger.Information("Manifest request code: {Code}", manifestCodeResponse.Body.manifest_request_code);

        var cdnClient = new SteamKit2.CDN.Client(_steamClient);

        var manifest = await cdnClient.DownloadManifestAsync(
            depotId: target.AppId,
            manifestId: target.ManifestId,
            manifestRequestCode: manifestCodeResponse.Body.manifest_request_code,
            server: target.Server,
            depotKey: target.DepotKey);

        Logger.Information("Manifest resolved. Files={Count}", manifest.Files!.Count);

        Directory.CreateDirectory(outputPath);

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

            Logger.Information("Downloading {FileName} ({Size} bytes)", file.FileName, file.TotalSize);

            await using var fs = new FileStream(
                filePath, FileMode.Create, FileAccess.Write,
                FileShare.None, bufferSize: 81920, useAsync: true);

            fs.SetLength((long)file.TotalSize);

            foreach (var chunk in file.Chunks.OrderBy(c => c.Offset))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var destination = new byte[chunk.UncompressedLength];

                await cdnClient.DownloadDepotChunkAsync(
                    target.AppId, chunk, target.Server, destination, target.DepotKey);

                fs.Seek((long)chunk.Offset, SeekOrigin.Begin);
                await fs.WriteAsync(destination, cancellationToken);
            }
        }

        Logger.Information("Download complete. output={OutputPath}", outputPath);
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
        }
    }

    private static void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        Logger.Information("Logged off of Steam: {Result}", callback.Result);
    }
}