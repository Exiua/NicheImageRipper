using System.Text.Json;
using System.Text.Json.Nodes;
using Serilog;

namespace NicheImageRipper.Sdk.Configuration;

/// <summary>
///     One-time migrations for config.json files written by older versions:
///     <list type="bullet">
///         <item>per-site config (Logins/Cookies/Keys/Custom) moving from fixed properties to
///         Dictionary&lt;string, T&gt; keyed by parser name;</item>
///         <item>individual settings whose JSON shape changed (see <see cref="MigrateLegacyValues"/>).</item>
///     </list>
///     Converts in memory; the caller is responsible for saving the result so this never needs to run again for
///     that file.
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

    private static readonly JsonSerializerOptions SerializerOptions = new() { AllowTrailingCommas = true };

    /// <summary>
    ///     Returns true and outputs a migrated config if the file at <paramref name="path"/> needed any migration.
    ///     Returns false if the file is already in the current shape, in which case the caller should deserialize
    ///     it normally.
    /// </summary>
    public static bool TryMigrate<T>(string path, out T migrated) where T : GeneralConfig, new()
    {
        return TryMigrateJson(File.ReadAllText(path), out migrated);
    }

    /// <inheritdoc cref="TryMigrate{T}"/>
    internal static bool TryMigrateJson<T>(string json, out T migrated) where T : GeneralConfig, new()
    {
        migrated = null!;

        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true })!
                           .AsObject();

        // Fix individual values first: deserializing throws on a value still in an old shape
        var valuesMigrated = MigrateLegacyValues(root);

        // Old shape: Logins/Cookies/Keys/Custom are JSON objects with fixed property names.
        // New shape: they're JSON objects too, but keyed by lowercase parser names — the reliable signal
        // is checking for a property name we know only exists in the old shape.
        var looksLegacy = root["Logins"] is JsonObject logins && logins.ContainsKey("DeviantArt");
        if (!looksLegacy && !valuesMigrated)
        {
            return false;
        }

        if (!looksLegacy)
        {
            migrated = JsonSerializer.Deserialize<T>(root.ToJsonString(), SerializerOptions)!;
            return true;
        }

        Logger.Information("Detected legacy config.json shape — migrating per-site config to the new format");

        // The old sections don't match the new property types (e.g. a cookie was a bare string, now a string[]),
        // so deserializing them throws. Detach them first and convert them by hand afterwards.
        var oldLogins = Detach(root, "Logins");
        var oldCookies = Detach(root, "Cookies");
        var oldKeys = Detach(root, "Keys");
        var oldCustom = Detach(root, "Custom");
        var config = JsonSerializer.Deserialize<T>(root.ToJsonString(), SerializerOptions)!;

#pragma warning disable CS0618 // Type or member is obsolete
        config.Logins = MigrateLogins(oldLogins);
        config.Cookies = MigrateCookies(oldCookies);
        config.Keys = MigrateKeys(oldKeys);
        config.Custom = MigrateCustom(oldCustom);
#pragma warning restore CS0618 // Type or member is obsolete

        migrated = config;
        return true;
    }

    /// <summary>
    ///     Rewrites, in place, settings whose JSON shape changed between versions so the current types can
    ///     deserialize them.
    /// </summary>
    /// <returns>true if anything was rewritten</returns>
    internal static bool MigrateLegacyValues(JsonObject root)
    {
        var changed = false;

        // PostDownloadAction was a [Flags] enum (None = 0, RemoveDuplicates = 1) before it was split into file- and
        // directory-level actions. RemoveDuplicates maps to the file-level action; the directory-level post-process
        // script is newer than the enum, so it starts off. A null value is also replaced, since the code expects
        // an object.
        if (root.TryGetPropertyValue(nameof(GeneralConfig.PostDownloadAction), out var postDownloadAction) &&
            postDownloadAction is null or JsonValue)
        {
            var flags = postDownloadAction is JsonValue value ? ReadLegacyPostDownloadFlags(value) : 0;
            root[nameof(GeneralConfig.PostDownloadAction)] = new JsonObject
            {
                [nameof(PostDownloadAction.DirectoryLevel)] = new JsonObject
                {
                    [nameof(PostDownloadActionDirectoryLevel.RunPostProcessScript)] = false,
                },
                [nameof(PostDownloadAction.FileLevel)] = new JsonObject
                {
                    [nameof(PostDownloadActionFileLevel.RemoveDuplicates)] = (flags & LegacyRemoveDuplicatesFlag) != 0,
                },
            };
            Logger.Information("Migrated legacy PostDownloadAction value {Value} to file/directory-level actions",
                postDownloadAction?.ToJsonString() ?? "null");
            changed = true;
        }

        return changed;
    }

    private const int LegacyRemoveDuplicatesFlag = 1 << 0;

    private static JsonObject? Detach(JsonObject root, string propertyName)
    {
        if (!root.TryGetPropertyValue(propertyName, out var node))
        {
            return null;
        }

        root.Remove(propertyName);
        return node as JsonObject;
    }

    /// <summary>Reads the old flags enum, written as a number or (with a string enum converter) as flag names.</summary>
    private static int ReadLegacyPostDownloadFlags(JsonValue value)
    {
        if (value.TryGetValue<int>(out var number))
        {
            return number;
        }

        if (!value.TryGetValue<string>(out var text))
        {
            Logger.Warning("Unrecognized legacy PostDownloadAction value {Value}, using defaults", value.ToJsonString());
            return 0;
        }

        if (int.TryParse(text, out number))
        {
            return number;
        }

        var flags = 0;
        foreach (var name in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (name.Equals("RemoveDuplicates", StringComparison.OrdinalIgnoreCase))
            {
                flags |= LegacyRemoveDuplicatesFlag;
            }
            else if (!name.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning("Unrecognized legacy PostDownloadAction flag {Flag}, ignoring", name);
            }
        }

        return flags;
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