using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Sdk.Tests;

public class SubParserOverridesTest
{
    private const string Callee = "callee";
    private const string Other = "other";

    private static GeneralConfig CreateConfig(SubParserOverrideMode mode)
    {
        var config = new GeneralConfig
        {
            SubParserOverrides = mode,
            FilenameScheme = FilenameScheme.Original,
            GalleryUpdateMode = GalleryUpdateMode.UpdatesOnly,
        };
        config.GetOrAddSiteConfig(Callee).Overrides = new SettingsOverride
        {
            FilenameScheme = FilenameScheme.Chronological,
            GalleryUpdateMode = GalleryUpdateMode.Full,
            GalleryUpdateStrategy = default(GalleryUpdateStrategy),
        };
        return config;
    }

    // The caller uses FilenameScheme.Hash and callerMode; the callee overrides Chronological/Full; globals are Original/UpdatesOnly
    [Theory]
    [InlineData(SubParserOverrideMode.Own, Callee, GalleryUpdateMode.UpdatesOnly, FilenameScheme.Chronological, GalleryUpdateMode.Full)]
    [InlineData(SubParserOverrideMode.Own, Other, GalleryUpdateMode.Full, FilenameScheme.Hash, GalleryUpdateMode.Full)]
    [InlineData(SubParserOverrideMode.Inherit, Callee, GalleryUpdateMode.UpdatesOnly, FilenameScheme.Hash, GalleryUpdateMode.UpdatesOnly)]
    [InlineData(SubParserOverrideMode.None, Callee, GalleryUpdateMode.Full, FilenameScheme.Original, GalleryUpdateMode.UpdatesOnly)]
    public void ResolvesNonSiteSpecificSettings(SubParserOverrideMode mode, string callee, GalleryUpdateMode callerMode,
                                                FilenameScheme expectedScheme, GalleryUpdateMode expectedMode)
    {
        var config = CreateConfig(mode);

        Assert.Equal(expectedScheme, config.GetSubParserFilenameScheme(callee, FilenameScheme.Hash));
        Assert.Equal(expectedMode, config.GetSubParserGalleryUpdateMode(callee, callerMode));
    }

    [Theory]
    [InlineData(SubParserOverrideMode.Own, true)]
    [InlineData(SubParserOverrideMode.Inherit, true)]
    [InlineData(SubParserOverrideMode.None, false)]
    public void UpdateStrategyIsNeverInherited(SubParserOverrideMode mode, bool expectCalleeStrategy)
    {
        var config = CreateConfig(mode);

        Assert.Equal(expectCalleeStrategy, config.GetSubParserGalleryUpdateStrategy(Callee) is not null);
        Assert.Null(config.GetSubParserGalleryUpdateStrategy(Other));
    }
}
