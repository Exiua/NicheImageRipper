using System.Text.Json.Serialization;

namespace CSWebDriverClient.Models.Requests;

public class PressButtonOnPageRequest
{
    [JsonPropertyName("button_xpath")]
    public string ButtonXPath { get; set; }
    [JsonPropertyName("timeout")]
    public float Timeout { get; set; }

    public PressButtonOnPageRequest(string buttonXPath, float timeout = 5)
    {
        ButtonXPath = buttonXPath;
        Timeout = timeout;
    }
}
