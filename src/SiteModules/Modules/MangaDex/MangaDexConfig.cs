using System.Text.Json;
using NicheImageRipper.Sdk.Configuration;
using Serilog;

namespace NicheImageRipper.SiteModules.Modules.MangaDex;

/// <summary>
///     MangaDex settings, read from the <c>mangadex</c> site config's Custom JSON, e.g. <c>{ "Language": "ja" }</c>.
/// </summary>
public class MangaDexConfig
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    ///     Chapter translation to download, as a MangaDex language code (e.g. <c>en</c>, <c>ja</c>, <c>es-la</c>,
    ///     <c>pt-br</c>). Also the preferred language for the folder name, falling back to English.
    /// </summary>
    [SiteSetting("Language", Description = "MangaDex language code of the chapters to download, e.g. en, ja, es-la, pt-br")]
    public string Language { get; set; } = "en";

    /// <summary>Reads the config, falling back to defaults when it's missing or malformed.</summary>
    public static MangaDexConfig From(JsonElement? custom)
    {
        if (custom is not { ValueKind: JsonValueKind.Object } json)
        {
            return new MangaDexConfig();
        }

        try
        {
            var config = json.Deserialize<MangaDexConfig>(Options) ?? new MangaDexConfig();
            if (string.IsNullOrWhiteSpace(config.Language))
            {
                config.Language = "en";
            }

            config.Language = config.Language.Trim();
            return config;
        }
        catch (JsonException e)
        {
            Log.ForContext<MangaDexConfig>().Warning(e, "Invalid MangaDex site config, using defaults");
            return new MangaDexConfig();
        }
    }
}
