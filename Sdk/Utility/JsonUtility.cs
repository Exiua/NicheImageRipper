using System.Text.Json;
using Sdk.Common.ExtensionMethods;
using Sdk.Exceptions;

namespace Sdk.Utility;

public static class JsonUtility
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
    };
    
    public static void Serialize(string filepath, object obj)
    {
        var json = JsonSerializer.Serialize(obj, obj.GetType(), Options);
        File.WriteAllText(filepath, json);
    }

    public static T? Deserialize<T>(string filepath)
    {
        var json = File.ReadAllText(filepath);
        return JsonSerializer.Deserialize<T>(json, Options);
    }
    
    public static string ExtractJsonObject(string json)
    {
        var depth = 0;
        var escaped = false;
        var inString = false;
        foreach (var (i, c) in json.Enumerate())
        {
            if (escaped)
            {
                escaped = false;
            }
            else
            {
                switch (c)
                {
                    case '\\':
                        escaped = true;
                        break;
                    case '{':
                        if (!inString)
                        {
                            depth++;
                        }

                        break;
                    case '}':
                        if (!inString)
                        {
                            depth--;
                        }

                        break;
                    case '"':
                        inString = !inString;
                        break;
                }
            }

            if (depth == 0)
            {
                return json[..(i + 1)];
            }
        }

        throw new RipperException($"Improperly formatted json: {json}");
    }
}