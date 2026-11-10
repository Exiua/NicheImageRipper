using System.Text.Json;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.SiteModules.Modules.DotParty;

namespace SiteModules.Tests;

public class DotPartySettingsTest
{
    private static DotPartySettings Read(string json) => SiteSettings.Read<DotPartySettings>(JsonDocument.Parse(json).RootElement);

    [Fact]
    public void MatchesServiceAndUserIdCaseInsensitively()
    {
        var settings = Read("""
            { "CreatorFilenameSchemes": [
                { "Service": "patreon", "UserId": "123", "FilenameScheme": "Hash" },
                { "Service": "fanbox", "UserId": "123", "FilenameScheme": "Chronological" }
            ] }
            """);

        Assert.Equal(FilenameScheme.Hash, settings.GetFilenameScheme("Patreon", "123"));
        Assert.Equal(FilenameScheme.Chronological, settings.GetFilenameScheme("fanbox", "123"));
        Assert.Null(settings.GetFilenameScheme("patreon", "456"));
    }

    [Fact]
    public void LaterEntriesWin()
    {
        var settings = Read("""
            { "CreatorFilenameSchemes": [
                { "Service": "patreon", "UserId": "1", "FilenameScheme": "Hash" },
                { "Service": "patreon", "UserId": "1", "FilenameScheme": "Original" }
            ] }
            """);

        Assert.Equal(FilenameScheme.Original, settings.GetFilenameScheme("patreon", "1"));
    }

    [Fact]
    public void SchemaDescribesCreatorList()
    {
        var field = SiteSettingsSchema.For(typeof(KemonoParser))!.Fields.Single();

        Assert.Equal(SiteSettingKind.ObjectList, field.Kind);
        Assert.Equal(["Service", "UserId", "FilenameScheme"], field.ItemFields!.Select(f => f.Key));
    }
}
