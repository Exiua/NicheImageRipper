using System.Text.Json.Serialization;

namespace FlareSolverrIntegration.Payloads;

public class GetRequestPayload : CommandPayload
{
    [JsonPropertyName("cmd")]
    public override string Command { get; set; } = "request.get";

    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;

    [JsonPropertyName("session")]
    public string? Session { get; set; }

    [JsonPropertyName("session_ttl_minutes")]
    public int? SessionTtlMinutes { get; set; }

    [JsonPropertyName("maxTimeout")]
    public int MaxTimeout { get; set; } = 60000;

    [JsonPropertyName("cookies")]
    public List<Dictionary<string, string>>? Cookies { get; set; }

    [JsonPropertyName("returnOnlyCookies")]
    public bool ReturnOnlyCookies { get; set; } = false;

    [JsonPropertyName("proxy")]
    public Dictionary<string, string>? Proxy { get; set; }
    
    public static GetRequestPayload SetUrl(string url)
    {
        return new GetRequestPayload
        {
            Url = url
        };
    }
    
    public GetRequestPayload SetSession(string session)
    {
        Session = session;
        return this;
    }
    
    public GetRequestPayload SetSessionTtlMinutes(int sessionTtlMinutes)
    {
        SessionTtlMinutes = sessionTtlMinutes;
        return this;
    }
    
    public GetRequestPayload SetMaxTimeout(int maxTimeout)
    {
        MaxTimeout = maxTimeout;
        return this;
    }
    
    public GetRequestPayload SetCookies(List<Dictionary<string, string>> cookies)
    {
        Cookies = cookies;
        return this;
    }
    
    public GetRequestPayload SetReturnOnlyCookies(bool returnOnlyCookies)
    {
        ReturnOnlyCookies = returnOnlyCookies;
        return this;
    }
    
    public GetRequestPayload SetProxy(Dictionary<string, string> proxy)
    {
        Proxy = proxy;
        return this;
    }
}
