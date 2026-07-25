using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Managers;
using Serilog;

namespace NicheImageRipper.Core.FileDownloading;

public sealed class DownloadContext
{
    public required Dictionary<string, string> RequestHeaders { get; init; }
    public required ApiClientManager ClientManager { get; init; }
    public required WebDriver WebDriver { get; init; }
    public required HttpClient Session { get; init; }
    public required string SiteName { get; init; }
    public required float SleepTime { get; init; }
    public required ILogger Logger { get; init; }
    public required IReadOnlyList<IRequestHeaderModifier> HeaderModifiers { get; init; }
    public required IReadOnlyList<IDownloadErrorHandler> ErrorHandlers { get; init; }
    public required IReadOnlyList<IPostDownloadValidator> PostDownloadValidators { get; init; }
}