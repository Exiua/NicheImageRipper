namespace NicheImageRipper.Sdk.Configuration;

/// <summary>What re-ripping a gallery from an <see cref="SiteParsing.IUpdatableHtmlParser"/> downloads.</summary>
public enum GalleryUpdateMode
{
    /// <summary>Only download what changed since the last rip (per the parser's update strategy).</summary>
    UpdatesOnly,

    /// <summary>Download the whole gallery again.</summary>
    Full,
}
