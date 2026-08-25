using NicheImageRipper.Core.Enums;
using Sdk.Enums;

namespace NicheImageRipper.Service.Models.Configs;

public class SettingsOverride
{
    public FilenameScheme? FilenameScheme { get; set; }

    public static SettingsOverride FromCoreSettingsOverride(Sdk.Configuration.SettingsOverride settingsOverride)
    {
        var overrides = new SettingsOverride
        {
            FilenameScheme = settingsOverride.FilenameScheme
        };
        return overrides;
    }
}