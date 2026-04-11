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

    /// <summary>
    ///     Get request urls made by the page at the given URL
    /// </summary>
    /// <param name="url">The URL of the page to analyze</param>
    /// <param name="timeoutSeconds">The time in seconds to wait for the page to load</param>
    /// <returns>>A BaseResponse containing either a GetNetworkUrlsResponse or an ErrorResponse</returns>
    public async Task<BaseResponse> GetNetworkUrls(string url, double timeoutSeconds = 5.0)
    {
        var request = new GetNetworkUrlsRequest(url, timeoutSeconds);
        return await PostRequest<GetNetworkUrlsRequest, GetNetworkUrlsResponse>("get_network_urls", request);
    }
    
    /// <summary>
    ///     Get the HTML content of a page at the given URL
    /// </summary>
    /// <param name="url">The URL of the page to retrieve</param>
    /// <param name="timeoutSeconds">The time in seconds to wait for the page to load</param>
    /// <param name="cookies">Optional cookies to include in the request</param>
    /// <param name="waitForXPath">Optional XPath to wait for before considering the page loaded</param>
    /// <returns>>A BaseResponse containing either a PageResponse or an ErrorResponse</returns>
    public Task<BaseResponse> GetPage(string url, double timeoutSeconds = 5.0, Dictionary<string, string>? cookies = null, string waitForXPath = "")
    {
        var request = new GetPageRequest(url, timeoutSeconds, cookies, waitForXPath);
        return PostRequest<GetPageRequest, PageResponse>("get_page", request);
    }
    
    /// <summary>
    ///     Press a button on the currently loaded page identified by its XPath
    /// </summary>
    /// <param name="buttonXPath">The XPath of the button to press</param>
    /// <param name="timeoutSeconds">The time in seconds to wait for the action to complete</param>
    /// <returns>>A BaseResponse containing either a PageResponse or an ErrorResponse</returns>
    public Task<BaseResponse> PressButtonOnPage(string buttonXPath, double timeoutSeconds = 5.0)
    {
        var request = new PressButtonOnPageRequest(buttonXPath, timeoutSeconds);
        return PostRequest<PressButtonOnPageRequest, PageResponse>("press_button_on_page", request);
    }
    
    private async Task<BaseResponse> PostRequest<TRequest, TResponse>(string endpoint, TRequest request)
        where TResponse : BaseResponse
    {
        var response = await _client.PostAsJsonAsync($"{_endpoint}/{endpoint}", request);
        if (!response.IsSuccessStatusCode)
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return errorResponse ?? new ErrorResponse { Error = "Unknown error occurred." };
        }
        
        var successResponse = await response.Content.ReadFromJsonAsync<TResponse>();
        if (successResponse is null)
        {
            return new ErrorResponse { Error = "Failed to parse response." };
        }

        return successResponse;
    }
}