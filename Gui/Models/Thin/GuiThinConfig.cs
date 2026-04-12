using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gui.Models.Thin;

public class GuiThinConfig
{
    public string ApiKey { get; set; } = "";
    public string EndpointUri { get; set; } = "";
    
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraData { get; set; }
}