namespace NicheImageRipper.Core.SiteParsing;

public interface ISubdomainSignificantHtmlParser
{
    /// <summary>How many trailing domain labels make up this site's name (e.g. 3 for "danbooru.donmai.us" -> "danbooru").</summary>
    static abstract int SignificantDomainLabels { get; }
}