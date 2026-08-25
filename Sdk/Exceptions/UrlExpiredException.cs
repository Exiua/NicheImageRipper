namespace Sdk.Exceptions;

public class UrlExpiredException(string siteName) : RipperException($"Links expired for site: {siteName}")
{
    public string SiteName { get; } = siteName;
    public int ResumeIndex { get; set; }
}