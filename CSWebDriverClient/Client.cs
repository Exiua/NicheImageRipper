using System.Net.Http.Json;
using CSWebDriverClient.Models.Requests;
using CSWebDriverClient.Models.Responses;

namespace CSWebDriverClient;

public class Client
{
    private readonly string _endpoint;
    private readonly HttpClient _client;
    
    public Client(string endpoint)
    {
        _endpoint = endpoint.EndsWith('/') ? endpoint.TrimEnd('/') : endpoint;
        _client = new HttpClient();
    }

    public async Task<BaseResponse> GetNetworkUrls(string url, double timeoutSeconds = 5.0)
    {
        var request = new GetNetworkUrlsRequest(url, timeoutSeconds);
        var response = await _client.PostAsJsonAsync($"{_endpoint}/get_network_urls", request);
        if (!response.IsSuccessStatusCode)
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return errorResponse ?? new ErrorResponse { Error = "Unknown error occurred." };
        }
        
        var successResponse = await response.Content.ReadFromJsonAsync<GetNetworkUrlResponse>();
        if (successResponse is null)
        {
            return new ErrorResponse { Error = "Failed to parse response." };
        }

        return successResponse;
    }
}