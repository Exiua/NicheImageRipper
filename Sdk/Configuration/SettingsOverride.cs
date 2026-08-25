using Sdk.Enums;

namespace Sdk.Configuration;

public class SettingsOverride
{
    public FilenameScheme FilenameScheme { get; set; } = FilenameScheme.Original;
}