using Gui.Models;
using NicheImageRipper.Core.Enums;

namespace Gui.Services.Full;

public class FullRipperSettings : IRipperSettings
{
    private static GuiConfig Config => (GuiConfig) NicheImageRipper.Core.Configuration.Config.Instance;
    
    public string SavePath
    {
        get => Config.SavePath;
        set => Config.SavePath = value;
    }

    public FilenameScheme FilenameScheme
    {
        get => Config.FilenameScheme;
        set => Config.FilenameScheme = value;
    }

    public UnzipProtocol UnzipProtocol
    {
        get => Config.UnzipProtocol;
        set => Config.UnzipProtocol = value;
    }

    public int MaxRetries
    {
        get => Config.MaxRetries;
        set => Config.MaxRetries = value;
    }
    public int RetryDelay
    {
        get => Config.RetryDelay;
        set => Config.RetryDelay = value;
    }
    public bool SkipFailedDownloads
    {
        get => Config.SkipFailedDownloads;
        set => Config.SkipFailedDownloads = value;
    }
}