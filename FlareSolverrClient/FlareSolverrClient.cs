using System.Net.Http.Json;
using FlareSolverrClient.Payloads;
using FlareSolverrClient.Responses;

namespace FlareSolverrClient;

public class FlareSlolverrClient
{
    private readonly HttpClient _client;
    private readonly string _flareSolverrUri;
    
    public FlareSlolverrClient(string flareSolverrUri)
    {
        _client = new HttpClient();
        _flareSolverrUri = flareSolverrUri;
    }

    public async Task<BaseResponse> CreateSession(string? sessionId = null, Dictionary<string, string>? proxy = null)
    {
        var payload = new CreateSessionPayload
        {
            Session = sessionId,
            Proxy = proxy
        };
        
        var response = await _client.PostAsJsonAsync(_flareSolverrUri, payload);
        BaseResponse? responseData;
        if (!response.IsSuccessStatusCode)
        {
            responseData = await response.Content.ReadFromJsonAsync<BaseResponse>();
        }
        else
        {
            responseData = await response.Content.ReadFromJsonAsync<SessionCreationResponse>();
        }
        
        return responseData ?? throw new Exception("Failed to create session");
    }
    
    public async Task<BaseResponse> ListSessions()
    {
        var payload = new ListSessionsPayload();
        
        var response = await _client.PostAsJsonAsync(_flareSolverrUri, payload);
        BaseResponse? responseData;
        if (!response.IsSuccessStatusCode)
        {
            responseData = await response.Content.ReadFromJsonAsync<BaseResponse>();
        }
        else
        {
            responseData = await response.Content.ReadFromJsonAsync<SessionListResponse>();
        }
        
        return responseData ?? throw new Exception("Failed to list sessions");
    }
    
    public async Task<BaseResponse> DeleteSession(string sessionId)
    {
        var payload = new DeleteSessionPayload
        {
            Session = sessionId
        };
        
        var response = await _client.PostAsJsonAsync(_flareSolverrUri, payload);
        return await response.Content.ReadFromJsonAsync<BaseResponse>() ?? throw new Exception("Failed to close session");
    }
    
    public async Task<BaseResponse> GetRequest(GetRequestPayload payload)
    {
        var response = await _client.PostAsJsonAsync(_flareSolverrUri, payload);
        BaseResponse? responseData;
        if (!response.IsSuccessStatusCode)
        {
            responseData = await response.Content.ReadFromJsonAsync<BaseResponse>();
        }
        else
        {
            responseData = await response.Content.ReadFromJsonAsync<RequestResponse>();
        }
        
        return responseData ?? throw new Exception("Failed to make GET request");
    }
    
    public async Task<BaseResponse> PostRequest(PostRequestPayload payload)
    {
        var response = await _client.PostAsJsonAsync(_flareSolverrUri, payload);
        BaseResponse? responseData;
        if (!response.IsSuccessStatusCode)
        {
            responseData = await response.Content.ReadFromJsonAsync<BaseResponse>();
        }
        else
        {
            responseData = await response.Content.ReadFromJsonAsync<RequestResponse>();
        }
        
        return responseData ?? throw new Exception("Failed to make POST request");
    }
}