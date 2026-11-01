using System.Text.Json.Serialization;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Sdk.Configuration;

public class SettingsOverride
{
    public FilenameScheme FilenameScheme { get; set; } = FilenameScheme.Original;

    /// <summary>Overrides <see cref="GeneralConfig.GalleryUpdateMode"/> for this site; null uses the global setting.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GalleryUpdateMode? GalleryUpdateMode { get; set; }

    /// <summary>
    ///     Overrides an updatable parser's <see cref="SiteParsing.IUpdatableHtmlParser.DefaultUpdateStrategy"/>; null,
    ///     or a strategy the parser doesn't support, uses the parser's default.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GalleryUpdateStrategy? GalleryUpdateStrategy { get; set; }
}
