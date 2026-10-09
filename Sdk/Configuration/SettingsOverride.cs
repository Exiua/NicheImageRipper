using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Sdk.Configuration;

public class SettingsOverride
{
    public FilenameScheme FilenameScheme { get; set; } = FilenameScheme.Original;
}