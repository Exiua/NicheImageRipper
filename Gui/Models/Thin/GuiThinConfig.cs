using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Gui.Models.Thin;

public class GuiThinConfig
{
    private const string ConfigPath = "config.json";
    
    public static GuiThinConfig Instance { get; } = GetOrLoadConfig();
    
    public string ApiKey { get; set; } = "";
    public string EndpointUri { get; set; } = "";
    
    public double NameWidth { get; set; } = 530;
    public double UrlWidth { get; set; } = 530;
    public double DateWidth { get; set; } = 150;
    public double CountWidth { get; set; } = 100;
    
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraData { get; set; }

    private static GuiThinConfig GetOrLoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            return new GuiThinConfig();
        }

        var config = JsonUtility.Deserialize<GuiThinConfig>(ConfigPath);
        return config ?? new GuiThinConfig();

    }
}