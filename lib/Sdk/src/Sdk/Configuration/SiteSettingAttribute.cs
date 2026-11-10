namespace NicheImageRipper.Sdk.Configuration;

/// <summary>
///     Marks a property of a site's custom settings class (see <see cref="SiteParsing.ICustomSettingsHtmlParser"/>) as
///     editable in the GUI. Properties without it aren't shown, but values for them in the site config are kept.
/// </summary>
/// <param name="label">The name shown next to the field.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SiteSettingAttribute(string label) : Attribute
{
    public string Label { get; } = label;

    /// <summary>Help text shown with the field.</summary>
    public string? Description { get; init; }

    /// <summary>For string properties: masks the value like a password.</summary>
    public bool Secret { get; init; }

    /// <summary>For lists of objects: what one entry is called, e.g. "Creator" for an "Add Creator" button.</summary>
    public string? ItemLabel { get; init; }
}
