using GuiThin.Models.Data;

namespace GuiThin.Services;

public sealed class LogStreamCoordinator
{
    private readonly IBackendConnector _backendConnector;
    private readonly ILogTextService _logTextService;

    public LogStreamCoordinator(IBackendConnector backendConnector, ILogTextService logTextService)
    {
        _backendConnector = backendConnector;
        _logTextService = logTextService;

        _backendConnector.LogReceived += OnLogReceived;
    }

    private void OnLogReceived(BackendLogEvent entry)
    {
        _logTextService.Append(entry);
    }
}