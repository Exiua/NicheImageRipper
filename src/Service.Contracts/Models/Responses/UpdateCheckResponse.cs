namespace NicheImageRipper.Service.Models.Responses;

/// <param name="CurrentVersion">Version of the running Core</param>
/// <param name="LatestVersion">Latest released version, or null if it couldn't be retrieved</param>
/// <param name="IsLatest">Result of NicheImageRipper.IsLatestVersion(); null if the latest version is unknown</param>
public record UpdateCheckResponse(Version CurrentVersion, Version? LatestVersion, bool? IsLatest);
