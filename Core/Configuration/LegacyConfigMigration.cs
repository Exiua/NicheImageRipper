using System.Text.Json;
using System.Text.Json.Nodes;
using Serilog;

namespace NicheImageRipper.Core.Configuration;

/// <summary>
///     One-time migration for config.json files written before per-site config (Logins/Cookies/Keys/Custom)
///     moved from fixed properties to Dictionary&lt;string, T&gt; keyed by parser name. Detects the old shape
///     and converts in memory; the caller is responsible for saving the result so this never needs to run
///     again for that file.
/// </summary>
internal static class LegacyConfigMigration
{
    private static readonly ILogger Logger = Log.ForContext(typeof(LegacyConfigMigration));

    // old fixed-property name -> new dictionary key (IHtmlParser.ParserName)
    private static readonly Dictionary<string, string> LoginKeyMap = new()
    {
        ["DeviantArt"] = "deviantart",
        ["Mega"] = "mega",
        ["TitsInTops"] = "titsintops",
        ["Nijie"] = "nijie",
        ["Danbooru"] = "danbooru",
        ["Gelbooru"] = "gelbooru",
        ["Rule34"] = "rule34",
        ["Yandere"] = "yande.re",
        ["E621"] = "e621",
        ["E-Hentai"] = "e-hentai",
        ["SteamCommunity"] = "steamcommunity",
        ["Iwara"] = "iwara",
        ["Pornhub"] = "pornhub",
    };

    private static readonly Dictionary<string, string> CookieKeyMap = new()
    {
        ["Twitter"] = "x",
        ["Newgrounds"] = "newgrounds",
        ["Porn3dx"] = "porn3dx",
        ["Pornhub"] = "pornhub",
        ["Thothub"] = "thothub",
        ["Kemono"] = "kemono",
        ["SimpCity"] = "simpcity",
        ["Pixiv"] = "pixiv",
        ["SteamCommunity"] = "steamcommunity",
        ["Patreon"] = "patreon",
    };

    private static readonly Dictionary<string, string> KeyKeyMap = new()
    {
        ["Imgur"] = "imgur",
        ["Google"] = "gdrive",
        ["Dropbox"] = "dropbox",
        ["Pixeldrain"] = "pixeldrain",
        ["Pixiv"] = "pixiv",
    };

    // old fixed-property name (as it appears under "Custom") -> new dictionary key
    private static readonly Dictionary<string, string> CustomKeyMap = new()
    {
        ["V2PH"] = "v2ph",
        ["GoFile"] = "gofile",
    };

    /// <summary>
    ///     Returns true and outputs a migrated config if the file at <paramref name="path"/> is in the old
    ///     (pre-dictionary) shape. Returns false if the file is already in the current shape or doesn't need
    ///     migrating, in which case the caller should deserialize it normally.
    /// </summary>
    public static bool TryMigrate<T>(string path, out T migrated) where T : GeneralConfig, new()
    {
        migrated = null!;

        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        // Old shape: Logins/Cookies/Keys/Custom are JSON objects with fixed property names.
        // New shape: they're JSON objects too, but keyed by lowercase parser names — the reliable signal
        // is checking for a property name we know only exists in the old shape.
        var looksLegacy = root["Logins"] is JsonObject logins && logins.ContainsKey("DeviantArt");
        if (!looksLegacy)
        {
            return false;
        }

        Logger.Information("Detected legacy config.json shape — migrating per-site config to the new format");

        var config = JsonSerializer.Deserialize<T>(root.ToJsonString())!;
        // The above deserialize will have left Logins/Cookies/Keys/Custom empty or thrown away extra data,
        // since the old JSON shape doesn't match the new property types — re-read those sections manually
        // from the raw JsonObject instead.

        config.Logins = MigrateLogins(root["Logins"]?.AsObject());
        config.Cookies = MigrateCookies(root["Cookies"]?.AsObject());
        config.Keys = MigrateKeys(root["Keys"]?.AsObject());
        config.Custom = MigrateCustom(root["Custom"]?.AsObject());

        migrated = config;
        return true;
    }

    private static Dictionary<string, Credentials> MigrateLogins(JsonObject? old)
    {
        var result = new Dictionary<string, Credentials>();
        if (old is null)
        {
            return result;
        }

        foreach (var (oldKey, newKey) in LoginKeyMap)
        {
            if (old[oldKey] is not JsonObject entry)
            {
                continue;
            }

            var username = entry["Username"]?.GetValue<string>() ?? "";
            var password = entry["Password"]?.GetValue<string>() ?? "";
            if (username != "" || password != "")
            {
                result[newKey] = new Credentials { Username = username, Password = password };
            }
        }

        return result;
    }

    private static Dictionary<string, string[]> MigrateCookies(JsonObject? old)
    {
        var result = new Dictionary<string, string[]>();
        if (old is null)
        {
            return result;
        }

        foreach (var (oldKey, newKey) in CookieKeyMap)
        {
            var node = old[oldKey];
            if (node is null)
            {
                continue;
            }

            // Pixiv's old StringOrArrayConverter meant this could already be an array; every other site
            // was a bare string.
            var values = node is JsonArray array
                ? array.Select(n => n!.GetValue<string>()).ToArray()
                : [node.GetValue<string>()];

            if (values.Any(v => v != ""))
            {
                result[newKey] = values;
            }
        }

        return result;
    }

    private static Dictionary<string, string> MigrateKeys(JsonObject? old)
    {
        var result = new Dictionary<string, string>();
        if (old is null)
        {
            return result;
        }

        foreach (var (oldKey, newKey) in KeyKeyMap)
        {
            var value = old[oldKey]?.GetValue<string>() ?? "";
            if (value != "")
            {
                result[newKey] = value;
            }
        }

        return result;
    }

    private static Dictionary<string, JsonElement> MigrateCustom(JsonObject? old)
    {
        var result = new Dictionary<string, JsonElement>();
        if (old is null)
        {
            return result;
        }

        foreach (var (oldKey, newKey) in CustomKeyMap)
        {
            if (old[oldKey] is JsonObject entry)
            {
                result[newKey] = JsonSerializer.Deserialize<JsonElement>(entry.ToJsonString());
            }
        }

        return result;
    }
}