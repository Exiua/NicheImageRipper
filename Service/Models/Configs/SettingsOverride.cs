using NicheImageRipper.Core.Enums;

namespace Service.Models.Configs;

public class SettingsOverride
{
    public FilenameScheme? FilenameScheme { get; set; }

    public static SettingsOverride FromCoreSettingsOverride(NicheImageRipper.Core.Configuration.SettingsOverride settingsOverride)
    {
        var overrides = new SettingsOverride
        {
            FilenameScheme = settingsOverride.FilenameScheme
        };
        return overrides;
    }
}