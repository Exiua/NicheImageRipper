using System.Text.Json;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Sdk.Tests;

public class SiteSettingsSchemaTest
{
    private class Entry
    {
        [SiteSetting("Name")]
        public string Name { get; set; } = "";

        [SiteSetting("Scheme")]
        public FilenameScheme Scheme { get; set; } = FilenameScheme.Hash;
    }

    private class Settings
    {
        [SiteSetting("Token", Secret = true)]
        public string Token { get; set; } = "";

        [SiteSetting("Enabled", Description = "Turns it on")]
        public bool Enabled { get; set; } = true;

        [SiteSetting("Retries")]
        public int? Retries { get; set; } = 3;

        [SiteSetting("Tags")]
        public string[] Tags { get; set; } = [];

        [SiteSetting("Entries", ItemLabel = "Entry")]
        public List<Entry> Entries { get; set; } = [];

        public string NotEditable { get; set; } = "";
    }

    private abstract class BaseParser : ICustomSettingsHtmlParser
    {
        public static Type CustomSettingsType => typeof(Settings);
    }

    private class DerivedParser : BaseParser;

    [Fact]
    public void OnlyAttributedPropertiesBecomeFields()
    {
        var schema = SiteSettingsSchema.FromSettingsType(typeof(Settings));

        Assert.Equal(["Token", "Enabled", "Retries", "Tags", "Entries"], schema.Fields.Select(f => f.Key));
    }

    [Fact]
    public void KindsFollowPropertyTypes()
    {
        var kinds = SiteSettingsSchema.FromSettingsType(typeof(Settings)).Fields.Select(f => f.Kind);

        Assert.Equal([
            SiteSettingKind.Secret, SiteSettingKind.Toggle, SiteSettingKind.Integer, SiteSettingKind.TextList,
            SiteSettingKind.ObjectList,
        ], kinds);
    }

    [Fact]
    public void ObjectListsDescribeTheirItemsWithEnumDefaultsAsNames()
    {
        var entries = SiteSettingsSchema.FromSettingsType(typeof(Settings)).Fields.Single(f => f.Key == "Entries");

        Assert.Equal("Entry", entries.ItemLabel);
        var scheme = entries.ItemFields!.Single(f => f.Key == "Scheme");
        Assert.Equal(SiteSettingKind.Choice, scheme.Kind);
        Assert.Equal(Enum.GetNames<FilenameScheme>(), scheme.Choices!.Select(c => c.Name));
        Assert.Equal("Hash", scheme.DefaultValue!.Value.GetString());
    }

    [Fact]
    public void ForReadsTheSettingsTypeFromABaseParser()
    {
        Assert.NotNull(SiteSettingsSchema.For(typeof(DerivedParser)));
        Assert.Null(SiteSettingsSchema.For(typeof(object)));
    }

    [Fact]
    public void SchemaRoundTripsThroughJson()
    {
        var schema = SiteSettingsSchema.FromSettingsType(typeof(Settings));

        var copy = JsonSerializer.Deserialize<SiteSettingsSchema>(JsonSerializer.Serialize(schema))!;

        Assert.Equal(schema.Fields.Count, copy.Fields.Count);
        Assert.Equal(schema.Fields[^1].ItemFields!.Select(f => f.Key), copy.Fields[^1].ItemFields!.Select(f => f.Key));
    }

    [Fact]
    public void ReadAcceptsEnumNamesAndNumbersCaseInsensitively()
    {
        var json = JsonDocument.Parse("""{ "entries": [{ "name": "a", "scheme": "chronological" }, { "Scheme": 0 }] }""")
                               .RootElement;

        var settings = SiteSettings.Read<Settings>(json);

        Assert.Equal([FilenameScheme.Chronological, FilenameScheme.Original], settings.Entries.Select(e => e.Scheme));
    }

    [Fact]
    public void ReadFallsBackToDefaultsWhenMalformed()
    {
        var json = JsonDocument.Parse("""{ "Enabled": "not a bool" }""").RootElement;

        Assert.True(SiteSettings.Read<Settings>(json).Enabled);
    }
}
