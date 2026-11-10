using NicheImageRipper.Sdk.Configuration;

namespace NicheImageRipper.Sdk.SiteParsing;

/// <summary>
///     Implemented by parsers with site-specific settings, stored in their <see cref="SiteConfig.Custom"/> JSON. The GUI
///     builds an editor for them from <see cref="CustomSettingsType"/> (see <see cref="SiteSettingsSchema"/>) instead
///     of making the user write the JSON; the parser reads them with <see cref="SiteSettings.Read{T}(string)"/>.
/// </summary>
public interface ICustomSettingsHtmlParser
{
    /// <summary>
    ///     A class with a parameterless constructor whose <see cref="SiteSettingAttribute"/> properties are the editable
    ///     settings. Property initializers give the defaults.
    /// </summary>
    static abstract Type CustomSettingsType { get; }
}
