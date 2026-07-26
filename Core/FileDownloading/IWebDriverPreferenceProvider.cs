namespace NicheImageRipper.Core.FileDownloading;

public interface IWebDriverPreferenceProvider
{
    bool AppliesTo(string siteName);
    bool RequiresNonHeadless { get; }
}