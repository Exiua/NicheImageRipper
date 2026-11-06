using System.ComponentModel.DataAnnotations;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Service.Models.Configs;

/// <summary>A partial settings update: only non-null properties are applied.</summary>
public class Config : IValidatableObject
{
    public string? UserAgent { get; set; }
    [MinLength(1)]
    public string? SavePath { get; set; }
    public string? Theme { get; set; }
    public FilenameScheme? FilenameScheme { get; set; }
    public UnzipProtocol? UnzipProtocol { get; set; }
    public PostDownloadAction? PostDownloadAction { get; set; }
    public ReRipBehavior? ReRipBehavior { get; set; }
    public GalleryUpdateMode? GalleryUpdateMode { get; set; }
    public SubParserOverrideMode? SubParserOverrides { get; set; }
    public bool? LiveHistory { get; set; }
    public bool? SkipFailedDownloads { get; set; }
    [Range(1, int.MaxValue)]
    public int? NumThreads { get; set; }
    [Range(1, int.MaxValue)]
    public int? MaxRetries { get; set; }
    [Range(0, int.MaxValue)]
    public int? RetryDelay { get; set; }
    public string? FlareSolverrUri { get; set; }
    public bool? CloseFlareSolverrSession { get; set; }
    public string? CSWebDriverUri { get; set; }
    public bool? SaveUnfinishedUrls { get; set; }

    /// <summary>Per-site configs to replace. A null value removes that site's config.</summary>
    public Dictionary<string, SiteConfig?>? Sites { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsBlankOrHttpUri(FlareSolverrUri))
        {
            yield return new ValidationResult("Must be an absolute http(s) URI, or empty to disable it.",
                [nameof(FlareSolverrUri)]);
        }

        if (!IsBlankOrHttpUri(CSWebDriverUri))
        {
            yield return new ValidationResult("Must be an absolute http(s) URI, or empty to disable it.",
                [nameof(CSWebDriverUri)]);
        }
    }

    private static bool IsBlankOrHttpUri(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
    }
}
