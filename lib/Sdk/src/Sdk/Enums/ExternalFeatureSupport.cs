namespace NicheImageRipper.Sdk.Enums;

[Flags]
public enum ExternalFeatureSupport
{
    None            = 0,
    Ffmpeg          = 1 << 0,
    YtDlp           = 1 << 1,
    MegaSdk         = 1 << 2,
    FlareSolverr    = 1 << 3,
    CSWebDriver     = 1 << 4,
    SteamCmd        = 1 << 5,
}