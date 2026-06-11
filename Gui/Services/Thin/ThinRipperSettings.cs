using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;
using Core.Enums;
using Config = Service.Models.Configs.Config;

namespace Gui.Services.Thin;

public class ThinRipperSettings : IRipperSettings
{
    private readonly IBackendConnector _backendConnector;

    private GeneralConfig _config = new();
    private bool _initialized;
    private bool _loading;

    public ThinRipperSettings(IBackendConnector backendConnector)
    {
        _backendConnector = backendConnector;
    }

    public string SavePath
    {
        get => _config.SavePath;
        set
        {
            if (_config.SavePath == value)
            {
                return;
            }

            _config.SavePath = value;

            UpdateRemoteConfig(new Config
            {
                SavePath = value
            });
        }
    }

    public FilenameScheme FilenameScheme
    {
        get => _config.FilenameScheme;
        set
        {
            if (_config.FilenameScheme == value)
            {
                return;
            }

            _config.FilenameScheme = value;

            UpdateRemoteConfig(new Config
            {
                FilenameScheme = value
            });
        }
    }

    public UnzipProtocol UnzipProtocol
    {
        get => _config.UnzipProtocol;
        set
        {
            if (_config.UnzipProtocol == value)
            {
                return;
            }

            _config.UnzipProtocol = value;

            UpdateRemoteConfig(new Config
            {
                UnzipProtocol = value
            });
        }
    }

    public int MaxRetries
    {
        get => _config.MaxRetries;
        set
        {
            if (_config.MaxRetries == value)
            {
                return;
            }

            _config.MaxRetries = value;

            UpdateRemoteConfig(new Config
            {
                MaxRetries = value
            });
        }
    }

    public int RetryDelay
    {
        get => _config.RetryDelay;
        set
        {
            if (_config.RetryDelay == value)
            {
                return;
            }

            _config.RetryDelay = value;

            UpdateRemoteConfig(new Config
            {
                RetryDelay = value
            });
        }
    }

    public bool SkipFailedDownloads
    {
        get => _config.SkipFailedDownloads;
        set
        {
            if (_config.SkipFailedDownloads == value)
            {
                return;
            }

            _config.SkipFailedDownloads = value;

            UpdateRemoteConfig(new Config
            {
                SkipFailedDownloads = value
            });
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        _loading = true;

        try
        {
            _config = await _backendConnector.GetConfigAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _loading = false;
        }
    }

    private void UpdateRemoteConfig(Config configPatch)
    {
        if (!_initialized || _loading)
        {
            return;
        }

        _ = UpdateRemoteConfigAsync(configPatch);
    }

    private async Task UpdateRemoteConfigAsync(Config configPatch, CancellationToken cancellationToken = default)
    {
        try
        {
            await _backendConnector.UpdateConfigAsync(configPatch, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}