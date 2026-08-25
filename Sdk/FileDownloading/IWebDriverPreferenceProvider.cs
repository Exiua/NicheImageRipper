namespace Sdk.FileDownloading;

public interface IWebDriverPreferenceProvider
{
    bool AppliesTo(string siteName);
    bool RequiresNonHeadless { get; }
}