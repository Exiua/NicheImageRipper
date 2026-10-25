namespace NicheImageRipper.Sdk.Cache;

/// <summary>
/// Declares that a type owns cache/state files that should be cleared by the user-facing "clear cache"
/// action. Implemented by parsers with their own on-disk cache (e.g. DotPartyParser's
/// dotpartyCache.json, SimpCityParser's simpcitycache.json) as well as by Core's own cache-owning
/// managers, so "clear cache" doesn't need to hardcode every file by name.
/// </summary>
public interface ICacheOwner
{
    /// <summary>
    /// Clears this owner's cache/state. Must be safe to call even if nothing is currently cached
    /// (i.e., implementations should swallow FileNotFoundException/etc) rather than require the caller to know whether
    /// anything was cached.
    /// </summary>
    static abstract void ClearCache();
}