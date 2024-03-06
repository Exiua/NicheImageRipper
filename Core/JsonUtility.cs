﻿using System.Text.Json;

namespace Core;

public class JsonUtility
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };
    
    public static void Serialize<T>(string filepath, T obj)
    {
        var json = JsonSerializer.Serialize(obj, Options);
        File.WriteAllText(filepath, json);
    }

    public static T Deserialize<T>(string filepath) where T : new()
    {
        var json = File.ReadAllText(filepath);
        return JsonSerializer.Deserialize<T>(json) ?? new T();
    }
}