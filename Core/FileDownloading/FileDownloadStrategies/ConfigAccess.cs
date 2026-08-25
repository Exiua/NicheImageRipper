using NicheImageRipper.Core.Configuration;
using Sdk.Configuration;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

internal static class ConfigAccess
{
    public static GeneralConfig Config => Sdk.Configuration.Config.Instance;
}