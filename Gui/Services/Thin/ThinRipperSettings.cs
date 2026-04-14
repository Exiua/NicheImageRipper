using Core.Configuration;
using Core.Enums;
using Config = Service.Models.Configs.Config;

namespace Gui.Services.Thin;

public class ThinRipperSettings : IRipperSettings
{
    private readonly GeneralConfig _config;
    private readonly IBackendConnector _backendConnector;
    
    public string SavePath
    {
        get => _config.SavePath;
        set
        {
            _config.SavePath = value;
            var configPatch = new Config
            {
                SavePath = value
            };
            
            _backendConnector.UpdateConfigAsync(configPatch).Wait();
        }
    }

    public FilenameScheme FilenameScheme
    {
        get => _config.FilenameScheme;
        set
        {
            _config.FilenameScheme = value;
            var configPatch = new Config
            {
                FilenameScheme = value
            };
            
            _backendConnector.UpdateConfigAsync(configPatch).Wait();
        }
    }

    public UnzipProtocol UnzipProtocol
    {
        get => _config.UnzipProtocol;
        set
        {
            _config.UnzipProtocol = value;
            var configPatch = new Config
            {
                UnzipProtocol = value
            };
            
            _backendConnector.UpdateConfigAsync(configPatch).Wait();
        }
    }

    public int MaxRetries
    {
        get => _config.MaxRetries;
        set
        {
            _config.MaxRetries = value;
            var configPatch = new Config
            {
                MaxRetries = value
            };
            
            _backendConnector.UpdateConfigAsync(configPatch).Wait();
        }
    }

    public int RetryDelay
    {
        get => _config.RetryDelay;
        set
        {
            _config.RetryDelay = value;
            var configPatch = new Config
            {
                RetryDelay = value
            };
            
            _backendConnector.UpdateConfigAsync(configPatch).Wait();
        }
    }

    public bool SkipFailedDownloads
    {
        get => _config.SkipFailedDownloads;
        set
        {
            _config.SkipFailedDownloads = value;
            var configPatch = new Config
            {
                SkipFailedDownloads = value
            };
            
            _backendConnector.UpdateConfigAsync(configPatch).Wait();
        }
    }

    public ThinRipperSettings(BackendConnector backendConnector)
    {
        _backendConnector = backendConnector;
        var settings = backendConnector.GetConfigAsync().Result;
        _config = settings;
    }
}