namespace NicheImageRipper.Sdk.Configuration;

/// <summary>
///     Which <see cref="SettingsOverride"/> a parser uses when another parser delegates to it (an embedded link, or a
///     whole-site alias). Site-specific settings, like the gallery update strategy, always come from the callee.
/// </summary>
public enum SubParserOverrideMode
{
    /// <summary>Use the callee's own overrides where it defines them, otherwise the caller's.</summary>
    Own,

    /// <summary>Use the caller's overrides, ignoring the callee's.</summary>
    Inherit,

    /// <summary>Ignore overrides entirely and use the global settings.</summary>
    None,
}
