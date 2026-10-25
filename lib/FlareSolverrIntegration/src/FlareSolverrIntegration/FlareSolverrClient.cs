using System.Net.Http.Json;
using FlareSolverrIntegration.Payloads;
using FlareSolverrIntegration.Responses;

namespace FlareSolverrIntegration;
public class FlareSolverrClient
{
    private readonly HttpClient _client;
    private readonly string _flareSolverrUri;
    public FlareSolverrClient(string flareSolverrUri)
    {
        _client = new HttpClient();
        _flareSolverrUri = flareSolverrUri;
    }

    public async Task<BaseResponse> CreateSession(string? sessionId = null, Dictionary<string, string>? proxy = null, CancellationToken cancellationToken = default)
    {
        var payload = new CreateSessionPayload
        {
            Session = sessionId,
            Proxy = proxy
        };
        var response = await _client.PostAsJsonAsync(_flareSolverrUri, payload, cancellationToken: cancellationToken);
        BaseResponse? responseData;
        if (!response.IsSuccessStatusCode)
        {
            responseData = await response.Content.ReadFromJsonAsync<BaseResponse>(cancellationToken: cancellationToken);
        }
        else
        {
            responseData = await response.Content.ReadFromJsonAsync<SessionCreationResponse>(cancellationToken: cancellationToken);
        }

        return responseData ?? throw new Exception("Failed to create session");
    }

    public async Task<BaseResponse> ListSessions(CancellationToken cancellationToken = default)
    {
        var payload = new ListSessionsPayload();
        var response = await _client.PostAsJsonAsync(_flareSolverrUri, payload, cancellationToken: cancellationToken);
        BaseResponse? responseData;
        if (!response.IsSuccessStatusCode)
        {
            responseData = await response.Content.ReadFromJsonAsync<BaseResponse>(cancellationToken: cancellationToken);
        }
        else
        {
            responseData = await response.Content.ReadFromJsonAsync<SessionListResponse>(cancellationToken: cancellationToken);
        }

        return responseData ?? throw new Exception("Failed to list sessions");
    }

    public async Task<BaseResponse> DeleteSession(string sessionId, CancellationToken cancellationToken = default)
    {
        var payload = new DeleteSessionPayload
        {
            Session = sessionId
        };
        var response = await _client.PostAsJsonAsync(_flareSolverrUri, payload, cancellationToken: cancellationToken);
        return await response.Content.ReadFromJsonAsync<BaseResponse>(cancellationToken: cancellationToken) ?? throw new Exception("Failed to close session");
    }

    public async Task<BaseResponse> GetRequest(GetRequestPayload payload, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync(_flareSolverrUri, payload, cancellationToken: cancellationToken);
        BaseResponse? responseData;
        if (!response.IsSuccessStatusCode)
        {
            responseData = await response.Content.ReadFromJsonAsync<BaseResponse>(cancellationToken: cancellationToken);
        }
        else
        {
            responseData = await response.Content.ReadFromJsonAsync<RequestResponse>(cancellationToken: cancellationToken);
        }

        return responseData ?? throw new Exception("Failed to make GET request");
    }

    public async Task<BaseResponse> PostRequest(PostRequestPayload payload, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync(_flareSolverrUri, payload, cancellationToken: cancellationToken);
        BaseResponse? responseData;
        if (!response.IsSuccessStatusCode)
        {
            responseData = await response.Content.ReadFromJsonAsync<BaseResponse>(cancellationToken: cancellationToken);
        }
        else
        {
            responseData = await response.Content.ReadFromJsonAsync<RequestResponse>(cancellationToken: cancellationToken);
        }

        return responseData ?? throw new Exception("Failed to make POST request");
    }
}