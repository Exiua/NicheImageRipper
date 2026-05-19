using System.Net.Http.Json;
using System.Text.Json;
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
        List<SteamKit2.CDN.Server> Servers
    );
    
    private sealed class ServerSelector(IReadOnlyList<SteamKit2.CDN.Server> servers)
    {
        private int _index;
        public SteamKit2.CDN.Server Current => servers[_index % servers.Count];
        public void Rotate() => _index++;
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
                ["max_servers"] = 20,
            });

        var servers = response["servers"].Children
                                         .Where(s => s["type"].AsString() is "SteamCache" or "CDN")
                                         .Where(s => s["https_support"].AsString() == "mandatory")
                                         .Select(s => (SteamKit2.CDN.Server)new System.Net.DnsEndPoint(
                                              s["vhost"].AsString() ?? s["host"].AsString()!, 443))
                                         .ToList();

        return new DepotDownloadTarget(
            AppId: appId,
            ManifestId: hcontentFile,
            DepotKey: depotKeyResult.DepotKey,
            Servers: servers);
    }

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

        var serverSelector = new ServerSelector(target.Servers);
        
        var manifest = await _cdnClient.DownloadManifestAsync(
            depotId: target.AppId,
            manifestId: target.ManifestId,
            manifestRequestCode: manifestCodeResponse.Body.manifest_request_code,
            server: serverSelector.Current,
            depotKey: target.DepotKey);

        Logger.Information("Manifest resolved. Found {Count} files.", manifest.Files!.Count);

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

                await DownloadChunkWithRetryAsync(target, chunk, fs, serverSelector, cancellationToken);
            }
        }

        Logger.Information("Download complete");
    }
    
    private async Task DownloadChunkWithRetryAsync(
        DepotDownloadTarget target,
        DepotManifest.ChunkData chunk,
        FileStream fs,
        ServerSelector serverSelector,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 5;
        var delay = TimeSpan.FromSeconds(30);
        var destination = new byte[chunk.UncompressedLength];

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await _cdnClient.DownloadDepotChunkAsync(
                    target.AppId, chunk, serverSelector.Current, destination, target.DepotKey);

                fs.Seek((long)chunk.Offset, SeekOrigin.Begin);
                await fs.WriteAsync(destination, cancellationToken);
                return;
            }
            catch (SteamKitWebRequestException ex) when (ex.StatusCode is
                                                             System.Net.HttpStatusCode.ServiceUnavailable or
                                                             System.Net.HttpStatusCode.TooManyRequests or
                                                             System.Net.HttpStatusCode.InternalServerError)
            {
                if (attempt == maxRetries - 1)
                {
                    throw;
                }

                Logger.Warning(
                    "CDN request failed with {Status} on {Host}, rotating server and retrying in {Delay}s (attempt {Attempt}/{Max})",
                    ex.StatusCode, serverSelector.Current.Host, delay.TotalSeconds, attempt + 1, maxRetries);

                serverSelector.Rotate();
                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
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
                    steamid   = steamId,
                    appid     = appId,
                    page      = page,
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