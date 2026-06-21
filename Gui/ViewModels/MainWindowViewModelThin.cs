using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NicheImageRipper.Gui.Models.Thin;
using NicheImageRipper.Gui.Services;
using NicheImageRipper.Gui.Services.Thin;

namespace NicheImageRipper.Gui.ViewModels;

public class MainWindowViewModelThin(
    ILogger<MainWindowViewModelThin> logger,
    IRipperClient ripperClient,
    IRipperSettings ripperSettings,
    IGuiSettings guiSettings,
    IBackendConnector backendConnector,
    ILogTextSource logTextSource)
    : MainWindowViewModelBase(ripperClient, ripperSettings, guiSettings, logTextSource, logger)
{
    private static readonly Version Version = new(1, 0, 0);
    
    private static GuiThinConfig Config => GuiThinConfig.Instance;

    private string _title = $"GuiThin v{Version}";
    
    public override string Title => _title;
    public override bool IsThinClient => true;

    private bool _initialized;

    protected override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }
        
        _initialized = true;
        await base.InitializeAsync(cancellationToken);
        await backendConnector.InitializeAsync(cancellationToken);
        var coreVersion = await backendConnector.GetCurrentVersionAsync(cancellationToken);
        _title = $"GuiThin v{Version} - Core v{coreVersion}";
        Active = false;
    }

    protected override async Task ConnectToRemote(CancellationToken cancellationToken = default)
    {
        if (Active)
        {
            return;
        }
        
        if (string.IsNullOrWhiteSpace(Config.ApiKey) ||
            string.IsNullOrWhiteSpace(Config.EndpointUri))
        {
            logger.LogWarning("No API Key or Endpoint URI provided");
            return;
        }

        try
        {
            backendConnector.ApiKey = Config.ApiKey;
            backendConnector.EndpointUri = Config.EndpointUri;

            // Cheap authenticated request to verify remote is reachable.
            var remoteVersion = await backendConnector.GetCurrentVersionAsync(cancellationToken);

            logger.LogInformation("Connected to remote backend. Core version: {Version}", remoteVersion);

            await InitializeAsync(cancellationToken);

            await backendConnector.ConnectWebSocketAsync(cancellationToken);

            Active = true;
            ConnectButtonDisplay = "Disconnect";

            OnUrlQueueUpdated();
        }
        catch (Exception ex)
        {
            Active = false;

            logger.LogError(ex, "Failed to connect to remote backend at {EndpointUri}", Config.EndpointUri);

            try
            {
                await backendConnector.DisconnectWebSocketAsync(cancellationToken);
            }
            catch
            {
                // Ignore cleanup failure after failed connection.
            }
        }
    }
}