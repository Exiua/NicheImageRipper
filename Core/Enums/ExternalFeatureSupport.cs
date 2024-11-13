namespace Core.Enums;

[Flags]
public enum ExternalFeatureSupport
{
    None    = 0,
    Ffmpeg  = 1 << 0,
    YtDlp   = 1 << 1,
    MegaCmd = 1 << 2,
}