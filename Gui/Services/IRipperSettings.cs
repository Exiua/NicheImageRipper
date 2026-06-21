using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Gui.Services;

public interface IRipperSettings : IAsyncInitialization
{
    public string SavePath { get; set; }
    public FilenameScheme FilenameScheme { get; set; }
    public UnzipProtocol UnzipProtocol { get; set; }
    public int MaxRetries { get; set; }
    public int RetryDelay { get; set; }
    public bool SkipFailedDownloads { get; set; }
}