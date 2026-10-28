using NicheImageRipper.Core.Enums;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Service.Models.Configs;

public class SettingsOverride
{
    public FilenameScheme? FilenameScheme { get; set; }
    public GalleryUpdateMode? GalleryUpdateMode { get; set; }

    public static SettingsOverride FromCoreSettingsOverride(Sdk.Configuration.SettingsOverride settingsOverride)
    {
        var overrides = new SettingsOverride
        {
            FilenameScheme = settingsOverride.FilenameScheme,
            GalleryUpdateMode = settingsOverride.GalleryUpdateMode,
        };
        return overrides;
    }
}