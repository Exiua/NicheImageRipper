using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;

namespace NicheImageRipper.Core.FileDownloading;

public interface IFileDownloadStrategy
{
    protected static GeneralConfig Config => NicheImageRipper.Config;
    
    IEnumerable<LinkInfo> HandlesLinkInfo { get; }
    bool SupportsPostProcessing => true; // default; override to false where an external tool owns the result

    /// <summary>
    ///     Only Success, SkipNotAFailure, or Failed should ever be returned — Retry is for internal use between
    ///     a strategy's own attempt/retry logic and itself; the caller does not retry on this class's behalf.
    /// </summary>
    Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                       CancellationToken cancellationToken = default);
}

public sealed class FileDownloadStrategyRegistry
{
    private readonly Dictionary<LinkInfo, IFileDownloadStrategy> _strategies;

    public FileDownloadStrategyRegistry(IEnumerable<IFileDownloadStrategy> strategies)
    {
        _strategies = new Dictionary<LinkInfo, IFileDownloadStrategy>();
        foreach (var strategy in strategies)
        {
            foreach (var linkInfo in strategy.HandlesLinkInfo)
            {
                _strategies[linkInfo] = strategy;
            }
        }
    }

    public IFileDownloadStrategy Resolve(LinkInfo linkInfo) =>
        _strategies.TryGetValue(linkInfo, out var strategy)
            ? strategy
            : throw new RipperException($"Unknown LinkInfo: {linkInfo}");
}