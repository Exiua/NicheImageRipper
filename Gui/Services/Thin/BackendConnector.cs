using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.DataStructures;
using Core.Utility;
using Gui.Models;
using Gui.Models.Thin;
using Service.Models.Dtos;
using Service.Models.Requests;

namespace Gui.Services.Thin;

public class BackendConnector(HttpClient httpClient, ApplicationState applicationState) : IBackendConnector, IDisposable
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

    public async Task<string[]> GetQueueSnapshotAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/queue");

        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<string[]>(JsonOptions, cancellationToken);

        return result ?? [];
    }

    public async Task<List<RejectedUrlInfoDto>> QueueUrlsAsync(
        string[] urls,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<string[], List<RejectedUrlInfoDto>>(
            HttpMethod.Post,
            "api/queue",
            urls,
            cancellationToken);
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

        using var request = CreateRequest(HttpMethod.Delete, "api/queue", urls);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(method, path);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException($"Server returned no {typeof(T).Name} value.");
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(method, path, payload);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException($"Server returned no {typeof(TResponse).Name} value.");
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

    public async Task ConnectLogsAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        _webSocket.Dispose();

        var socket = new ClientWebSocket();

        socket.Options.SetRequestHeader("X-API-Key", ApiKey);

        var endpointUri = new Uri(EndpointUri);
        await socket.ConnectAsync(endpointUri, cancellationToken);

        _webSocket = socket;

        _ = Task.Run(() => ReceiveLoopAsync(socket, cancellationToken), cancellationToken);
    }

    public async Task DisconnectLogsAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client disconnect",
                cancellationToken);
        }

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

                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(ms.ToArray());

            try
            {
                var logEvent = System.Text.Json.JsonSerializer.Deserialize<LogEntryModel>(json);

                if (logEvent is not null)
                {
                    LogReceived?.Invoke(logEvent);
                }
            }
            catch (Exception ex)
            {
                var fallback = new LogEntryModel
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Level = "Error",
                    RenderedMessage = "Malformed log payload received",
                    Exception = $"Raw: {json}\n\nError: {ex}"
                };

                LogReceived?.Invoke(fallback);
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