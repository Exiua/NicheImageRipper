using System;
using System.Threading.Tasks;
using Core.ExtensionMethods;
using Gui.Models.Thin;
using Gui.Services;
using Gui.Services.Thin;
using Serilog;

namespace Gui.ViewModels;

public class MainWindowViewModelThin(
    IRipperClient ripperClient,
    IRipperSettings ripperSettings,
    IGuiSettings guiSettings,
    IBackendConnector backendConnector,
    ILogTextSource logTextSource)
    : MainWindowViewModelBase(ripperClient, ripperSettings, guiSettings, logTextSource)
{
    private static readonly Version Version = new(1, 0, 0);
    
    private static GuiThinConfig Config => GuiThinConfig.Instance;

    private string _title = $"GuiThin v{Version}";
    
    public override string Title => _title;
    public override bool IsThinClient => true;

    private bool _initialized;
    
    public override async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }
        
        _initialized = true;
        await base.InitializeAsync();
        await backendConnector.InitializeAsync();
        var coreVersion = await backendConnector.GetCurrentVersionAsync();
        _title = $"GuiThin v{Version} - Core v{coreVersion}";
        Active = false;
    }

    protected override async Task ConnectToRemote()
    {
        if (Active)
        {
            return;
        }
        
        if (string.IsNullOrWhiteSpace(Config.ApiKey) ||
            string.IsNullOrWhiteSpace(Config.EndpointUri))
        {
            Log.Warning("No API Key or Endpoint URI provided");
            return;
        }

        try
        {
            backendConnector.ApiKey = Config.ApiKey;
            backendConnector.EndpointUri = Config.EndpointUri;

            // Cheap authenticated request to verify remote is reachable.
            var remoteVersion = await backendConnector.GetCurrentVersionAsync();

            Log.Information("Connected to remote backend. Core version: {Version}", remoteVersion);

            await InitializeAsync();

            await backendConnector.ConnectWebSocketAsync();

            Active = true;
            ConnectButtonDisplay = "Disconnect";

            OnUrlQueueUpdated();
        }
        catch (Exception ex)
        {
            Active = false;

            Log.Error(ex, "Failed to connect to remote backend at {EndpointUri}", Config.EndpointUri);

            try
            {
                await backendConnector.DisconnectWebSocketAsync();
            }
            catch
            {
                // Ignore cleanup failure after failed connection.
            }
        }
    }
}