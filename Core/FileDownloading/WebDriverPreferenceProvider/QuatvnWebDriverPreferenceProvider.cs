namespace NicheImageRipper.Core.FileDownloading.WebDriverPreferenceProvider;

public sealed class QuatvnWebDriverPreferenceProvider : IWebDriverPreferenceProvider
{
    public bool AppliesTo(string siteName) => siteName == "quatvn";
    public bool RequiresNonHeadless => true;
}