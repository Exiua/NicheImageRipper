using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Reactive;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Utility;
using Gui.Models;
using Gui.Models.Thin;
using NicheImageRipper.Core.Configuration;
using Service.Models.Requests;
using Service.Models.WebSocket;
using Config = Service.Models.Configs.Config;

namespace Gui.Services.Thin;

public class BackendConnector(HttpClient httpClient) : IBackendConnector, IDisposable
{
    private const string ConfigFilename = "config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static GuiThinConfig Config { get; }

    private ClientWebSocket _webSocket = new();
    private bool _disposed;

    public string ApiKey
    {
        get => Config.ApiKey;
        set => Config.ApiKey = value;
    }

    public string EndpointUri
    {
        get => Config.EndpointUri;
        set => Config.EndpointUri = value;
    }

    public event Action<LogEntryModel>? LogReceived;
    public event Action? QueueUpdated;
    public event Action<int, int>? ProgressChanged;
    public event Action<string>? UnknownEventReceived;

    static BackendConnector()
    {
        if (File.Exists(ConfigFilename))
        {
            var config = JsonUtility.Deserialize<GuiThinConfig>(ConfigFilename);
            Config = config ?? new GuiThinConfig();
        }
        else
        {
            Config = new GuiThinConfig();
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(EndpointUri))
        {
            throw new InvalidOperationException("Endpoint URI is not configured.");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("API Key is not configured.");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        EnsureConfigured();

        var baseUri = new Uri(EndpointUri);
        var uri = new Uri(baseUri, path);

        var request = new HttpRequestMessage(method, uri);
        request.Headers.Remove("X-API-Key");
        request.Headers.Add("X-API-Key", ApiKey);

        return request;
    }

    private HttpRequestMessage CreateRequest<T>(HttpMethod method, string path, T payload)
    {
        var request = CreateRequest(method, path);
        var serializedPayload = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
        request.Content = content;
        return request;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(method, path);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException($"Server returned no {typeof(T).Name} value.");
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest payload, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(method, path, payload);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        if (typeof(TResponse) == typeof(Unit))
        {
            return (TResponse)(object)new Unit();
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException($"Server returned no {typeof(TResponse).Name} value.");
    }

    public async Task<string[]> GetQueueSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return await SendAsync<string[]>(HttpMethod.Get, "api/queue", cancellationToken);
    }

    public async Task<RejectedUrlsInfo> QueueUrlsAsync(
        string urls,
        bool force = false, CancellationToken cancellationToken = default)
    {
        var request = new QueueRequest
        {
            Urls = urls,
        };

        return await SendAsync<QueueRequest, RejectedUrlsInfo>(
            HttpMethod.Post,
            $"api/queue?force={force}",
            request);
    }

    public Task<int> GetQueueCountAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<int>(HttpMethod.Get, "api/queue/count", cancellationToken);
    }

    public async Task DequeueUrlsAsync(
        string[] urls,
        CancellationToken cancellationToken = default)
    {
        if (urls.Length == 0)
        {
            return;
        }

        await SendAsync<string[], Unit>(HttpMethod.Delete, "api/queue", urls);
        // using var request = CreateRequest(HttpMethod.Delete, "api/queue", urls);
        // using var response = await httpClient.SendAsync(request, cancellationToken);
        //
        // response.EnsureSuccessStatusCode();
    }

    public Task LoadUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        var urlList = urls as string[] ?? urls.ToArray();
        return SendAsync<string[], Unit>(HttpMethod.Post, "api/queue/load", urlList);
    }

    public Task<bool> RipAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<bool>(HttpMethod.Post, "api/state/rip", cancellationToken);
    }

    public Task<bool> PauseAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<bool>(HttpMethod.Post, "api/state/pause", cancellationToken);
    }

    public Task<bool> ResumeAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<bool>(HttpMethod.Post, "api/state/resume", cancellationToken);
    }

    public Task<bool> GetPausedStateAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<bool>(HttpMethod.Get, "api/state/paused", cancellationToken);
    }

    public Task<bool> GetIsRippingStateAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<bool>(HttpMethod.Get, "api/state/is-ripping", cancellationToken);
    }

    public Task<IEnumerable<HistoryEntry>> GetHistoryAsync(
        GetHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<IEnumerable<HistoryEntry>>(
            HttpMethod.Get,
            "api/history" + request.ToQuery(),
            cancellationToken);
    }

    public Task<int> GetHistoryCountAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<int>(
            HttpMethod.Get,
            "api/history/count",
            cancellationToken);
    }

    public async Task<GeneralConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var config = await SendAsync<GeneralConfig>(HttpMethod.Get, "api/settings", cancellationToken);
        return config;
    }

    public async Task UpdateConfigAsync(Config config, CancellationToken cancellationToken = default)
    {
        await SendAsync<Config, Unit>(HttpMethod.Patch, "api/settings", config);
    }

    public async Task<Version> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
    {
        var versionString = await SendAsync<string>(HttpMethod.Get, "api/version", cancellationToken);
        return Version.TryParse(versionString, out var version)
            ? version
            : throw new InvalidOperationException($"Server returned invalid version string: {versionString}");
    }

    public Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<Unit>(HttpMethod.Post, "api/state/clear-cache", cancellationToken);
    }

    public Task SaveStateAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<Unit>(HttpMethod.Post, "api/state/save", cancellationToken);
    }

    public async Task ConnectWebSocketAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        _webSocket.Dispose();

        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("X-API-Key", ApiKey);

        var baseUri = new Uri(EndpointUri.TrimEnd('/'));

        var wsUri = new UriBuilder(baseUri)
        {
            Scheme = baseUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
            Path = "ws/events"
        }.Uri;

        await socket.ConnectAsync(wsUri, cancellationToken);

        _webSocket = socket;
        //_connected = true;

        _ = Task.Run(() => ReceiveLoopAsync(socket, cancellationToken), cancellationToken);
    }

    public async Task DisconnectWebSocketAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client disconnect",
                cancellationToken);
        }

        //_connected = false;
        _webSocket.Dispose();
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var segment = new ArraySegment<byte>(buffer);

        while (!cancellationToken.IsCancellationRequested &&
               socket.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();

            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(segment, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        cancellationToken);

                    return;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(ms.ToArray());

            try
            {
                var envelope = JsonSerializer.Deserialize<WsEnvelope>(json);

                if (envelope is null)
                {
                    continue;
                }

                switch (envelope.EventType)
                {
                    case WsEventType.Log:
                    {
                        var logEvent = envelope.Payload.Deserialize<LogEntryModel>(options: JsonOptions);

                        if (logEvent is not null)
                        {
                            LogReceived?.Invoke(logEvent);
                        }

                        break;
                    }
                    case WsEventType.QueueUpdate:
                    {
                        QueueUpdated?.Invoke();
                        
                        break;
                    }
                    case WsEventType.ProgressChange:
                    {
                        var progressEvent = envelope.Payload.Deserialize<ProgressChangedEvent>();

                        if (progressEvent is not null)
                        {
                            ProgressChanged?.Invoke(progressEvent.Current, progressEvent.Total);
                        }

                        break;
                    }
                    case WsEventType.Unknown:
                    default:
                    {
                        UnknownEventReceived?.Invoke(json);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke(new LogEntryModel
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Level = "Error",
                    Message = "Malformed WebSocket payload received",
                    Exception = $"Raw: {json}\n\nError: {ex}"
                });
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _webSocket.Dispose();
        GC.SuppressFinalize(this);
    }
}