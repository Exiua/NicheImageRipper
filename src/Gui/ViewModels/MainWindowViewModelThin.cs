using NicheImageRipper.Sdk.Utility;
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NicheImageRipper.Gui.Models.Thin;
using NicheImageRipper.Gui.Services;
using NicheImageRipper.Gui.Services.Thin;
using NicheImageRipper.Service.Models.WebSocket;
using ReactiveUI;

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
    private static readonly Version Version = AssemblyVersionUtility.Of<MainWindowViewModelBase>();
    
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
        // Have the service send debug logs only when they're shown, rather than sending and hiding them
        this.WhenAnyValue(x => x.ShowDebugLogs)
            .Select(show => show ? WsLogLevel.Debug : WsLogLevel.Information)
            .DistinctUntilChanged()
            .Subscribe(level => _ = UpdateRemoteLogLevel(level));
        await backendConnector.InitializeAsync(cancellationToken);
        var coreVersion = await backendConnector.GetCurrentVersionAsync(cancellationToken);
        _title = $"GuiThin v{Version} - Core v{coreVersion}";
        Active = false;
    }

    private async Task UpdateRemoteLogLevel(WsLogLevel level)
    {
        try
        {
            await backendConnector.SetLogLevelAsync(level);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to change the remote log level to {LogLevel}", level);
        }
    }

    // Initialization needs the backend, so it happens in ConnectToRemote instead
    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    protected override async Task DisconnectFromRemote(CancellationToken cancellationToken = default)
    {
        if (!Active)
        {
            return;
        }

        try
        {
            await backendConnector.DisconnectWebSocketAsync(cancellationToken);
            logger.LogInformation("Disconnected from remote backend at {EndpointUri}", Config.EndpointUri);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while closing the connection to {EndpointUri}", Config.EndpointUri);
        }
        finally
        {
            Active = false;
            ConnectButtonDisplay = "Connect";
            ClearRemoteQueueDisplay();
        }
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