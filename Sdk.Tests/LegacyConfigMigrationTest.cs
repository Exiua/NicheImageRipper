using System.Text.Json.Nodes;
using NicheImageRipper.Sdk.Configuration;

namespace NicheImageRipper.Sdk.Tests;

public class LegacyConfigMigrationTest
{
    [Theory]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("\"None\"", false)]
    [InlineData("\"RemoveDuplicates\"", true)]
    [InlineData("\"None, RemoveDuplicates\"", true)]
    [InlineData("\"1\"", true)]
    public void LegacyPostDownloadActionIsMigrated(string legacyValue, bool expectedRemoveDuplicates)
    {
        var json = $$"""{ "SavePath": "./Rips/", "PostDownloadAction": {{legacyValue}}, "MaxRetries": 7 }""";

        var migrated = LegacyConfigMigration.TryMigrateJson<GeneralConfig>(json, out var config);

        Assert.True(migrated);
        Assert.Equal(expectedRemoveDuplicates, config.PostDownloadAction.FileLevel.RemoveDuplicates);
        Assert.False(config.PostDownloadAction.DirectoryLevel.RunPostProcessScript);
        // Everything else still deserializes normally
        Assert.Equal("./Rips/", config.SavePath);
        Assert.Equal(7, config.MaxRetries);
    }

    [Fact]
    public void NullPostDownloadActionGetsDefaults()
    {
        var migrated = LegacyConfigMigration.TryMigrateJson<GeneralConfig>("""{ "PostDownloadAction": null }""",
            out var config);

        Assert.True(migrated);
        Assert.NotNull(config.PostDownloadAction);
        Assert.False(config.PostDownloadAction.FileLevel.RemoveDuplicates);
    }

    [Fact]
    public void CurrentShapeIsLeftAlone()
    {
        const string json = """
                            {
                              "PostDownloadAction": {
                                "DirectoryLevel": { "RunPostProcessScript": true },
                                "FileLevel": { "RemoveDuplicates": true }
                              },
                            }
                            """;

        Assert.False(LegacyConfigMigration.TryMigrateJson<GeneralConfig>(json, out _));
    }

    [Fact]
    public void MissingPostDownloadActionIsLeftAlone()
    {
        Assert.False(LegacyConfigMigration.TryMigrateJson<GeneralConfig>("""{ "SavePath": "x" }""", out _));
    }

    [Fact]
    public void MigratesValuesAlongsideLegacyPerSiteShape()
    {
        const string json = """
                            {
                              "PostDownloadAction": 1,
                              "Logins": { "DeviantArt": { "Username": "user", "Password": "pass" } }
                            }
                            """;

        Assert.True(LegacyConfigMigration.TryMigrateJson<GeneralConfig>(json, out var config));
        config.MigrateLegacy();

        Assert.True(config.PostDownloadAction.FileLevel.RemoveDuplicates);
        Assert.Equal("user", config.GetSiteConfig("deviantart")?.Login?.Username);
    }

    [Fact]
    public void LegacyPerSiteSectionsWithOldValueTypesMigrate()
    {
        // Cookies were bare strings (except Pixiv), Keys plain strings, Custom fixed objects: none of these fit the
        // new dictionary value types, which used to make the migration itself throw
        const string json = """
                            {
                              "PostDownloadAction": 0,
                              "Logins": { "DeviantArt": { "Username": "", "Password": "" }, "Mega": { "Username": "m", "Password": "p" } },
                              "Keys": { "Imgur": "imgur-key", "Google": "" },
                              "Cookies": { "Twitter": "tw-cookie", "Pixiv": ["a", "b"], "Kemono": "" },
                              "Custom": { "GoFile": { "Token": "t" } },
                              "ParserSpecificSettingsOverrides": {}
                            }
                            """;

        Assert.True(LegacyConfigMigration.TryMigrateJson<GeneralConfig>(json, out var config));
        config.MigrateLegacy();

        Assert.Equal("m", config.GetSiteConfig("mega")?.Login?.Username);
        Assert.Null(config.GetSiteConfig("deviantart")); // empty credentials aren't carried over
        Assert.Equal("imgur-key", config.GetSiteConfig("imgur")?.Key);
        Assert.Equal(["tw-cookie"], config.GetSiteConfig("x")?.Cookies!);
        Assert.Equal(["a", "b"], config.GetSiteConfig("pixiv")?.Cookies!);
        Assert.Equal("t", config.GetSiteConfig("gofile")?.Custom?.GetProperty("Token").GetString());
        Assert.False(config.PostDownloadAction.FileLevel.RemoveDuplicates);
    }

    [Fact]
    public void MigrateLegacyValuesRewritesJsonInPlace()
    {
        var root = JsonNode.Parse("""{ "PostDownloadAction": 1 }""")!.AsObject();

        Assert.True(LegacyConfigMigration.MigrateLegacyValues(root));
        Assert.Equal(true, root["PostDownloadAction"]?["FileLevel"]?["RemoveDuplicates"]?.GetValue<bool>());
        Assert.False(LegacyConfigMigration.MigrateLegacyValues(root)); // idempotent once migrated
    }
}
