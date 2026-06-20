using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.Configuration;

public class SettingsOverride
{
    public FilenameScheme FilenameScheme { get; set; } = FilenameScheme.Original;
}