using NicheImageRipper.Core.Configuration;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

internal static class ConfigAccess
{
    public static GeneralConfig Config => Configuration.Config.Instance;
}