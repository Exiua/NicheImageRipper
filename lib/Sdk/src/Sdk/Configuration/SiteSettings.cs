using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace NicheImageRipper.Sdk.Configuration;

/// <summary>Reads a site's custom settings (<see cref="SiteConfig.Custom"/>) into its settings class.</summary>
public static class SiteSettings
{
    /// <summary>
    ///     Case-insensitive, with enums as names; how the GUI writes custom settings. Numeric enum values are still
    ///     accepted when reading.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The settings from the site config named <paramref name="parserName"/>.</summary>
    public static T Read<T>(string parserName) where T : new() =>
        Read<T>(Config.Instance.GetSiteConfig(parserName)?.Custom);

    /// <summary>Deserializes <paramref name="custom"/>, falling back to defaults when it's missing or malformed.</summary>
    public static T Read<T>(JsonElement? custom) where T : new()
    {
        if (custom is not { ValueKind: JsonValueKind.Object } json)
        {
            return new T();
        }

        try
        {
            return json.Deserialize<T>(JsonOptions) ?? new T();
        }
        catch (JsonException e)
        {
            Log.ForContext(typeof(SiteSettings)).Warning(e, "Invalid {SettingsType} site config, using defaults",
                typeof(T).Name);
            return new T();
        }
    }
}
