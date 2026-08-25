namespace Sdk.SiteParsing;

public interface IRefererOverrideHtmlParser
{
    /// <summary>Referer header to use instead of the default "https://{domain}/" for this site's URLs.</summary>
    static abstract string RefererOverride { get; }
}