using System.Collections.Concurrent;

namespace NicheImageRipper.Sdk.Features;

/// <summary>
/// Default in-memory <see cref="IAvailableFeatures"/> implementation.
/// </summary>
internal sealed class AvailableFeatureCache : IAvailableFeatures
{
    private readonly ConcurrentDictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);

    public bool HasFeature(string key, Func<bool> detect) => _cache.GetOrAdd(key, _ => detect());

    public bool HasFeature(string key) => _cache.GetValueOrDefault(key);
}