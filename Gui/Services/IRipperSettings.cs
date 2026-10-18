using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Gui.Services;

public interface IRipperSettings : IAsyncInitialization
{
    public string UserAgent { get; set; }
    public string SavePath { get; set; }
    public FilenameScheme FilenameScheme { get; set; }
    public UnzipProtocol UnzipProtocol { get; set; }
    public bool RemoveDuplicates { get; set; }
    public bool RunPostProcessScript { get; set; }
    public int MaxRetries { get; set; }
    public int RetryDelay { get; set; }
    public bool SkipFailedDownloads { get; set; }
    public bool SaveUnfinishedUrls { get; set; }
    public string FlareSolverrUri { get; set; }
    public bool CloseFlareSolverrSession { get; set; }
    public string CSWebDriverUri { get; set; }

    /// <summary>Current per-site configs keyed by parser name. Treat as read-only; use <see cref="SetSites"/>.</summary>
    public IReadOnlyDictionary<string, SiteConfig> Sites { get; }

    /// <summary>Replaces all per-site configs; sites missing from <paramref name="sites"/> are removed.</summary>
    public void SetSites(IReadOnlyDictionary<string, SiteConfig> sites);

    /// <summary>Parser names that can be given a site config.</summary>
    public Task<IReadOnlyList<string>> GetKnownSiteNames(CancellationToken cancellationToken = default);
}
