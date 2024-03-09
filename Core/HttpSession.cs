namespace Core;

public class HttpSession
{
    private HttpClient _client;
    private HttpSession? _session;
    
    public HttpSession Session => _session ??= new HttpSession();
    
    public Dictionary<string, string> Headers { get; set; } = new();
    
    private HttpSession()
    {
        
    }
    
    public async Task<HttpResponseMessage> GetAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var header in Headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }
        return await _client.SendAsync(request);
    }
}