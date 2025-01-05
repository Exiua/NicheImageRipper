using System.Text.Json.Serialization;

namespace FlareSolverrIntegration.Payloads;

public class PostRequestPayload : GetRequestPayload
{
    [JsonPropertyName("cmd")]
    public override string Command { get; set; } = "request.post";
    
    [JsonPropertyName("postData")]
    public string PostData { get; set; } = null!;
    
    public static PostRequestPayload SetUrlAndPostData(string url, string postData)
    {
        return new PostRequestPayload
        {
            Url = url,
            PostData = postData
        };
    }
}