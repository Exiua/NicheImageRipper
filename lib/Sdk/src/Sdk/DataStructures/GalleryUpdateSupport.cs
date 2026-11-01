using System.Reflection;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Sdk.DataStructures;

/// <summary>
///     The <see cref="GalleryUpdateStrategy"/>s an <see cref="IUpdatableHtmlParser"/> type supports, read from its
///     static members so it's known without constructing the parser (e.g. to list choices in a settings UI).
/// </summary>
/// <param name="Default">The parser's <see cref="IUpdatableHtmlParser.DefaultUpdateStrategy"/>.</param>
/// <param name="Supported">Every supported strategy, <paramref name="Default"/> first.</param>
public sealed record GalleryUpdateSupport(GalleryUpdateStrategy Default, IReadOnlyList<GalleryUpdateStrategy> Supported)
{
    /// <summary>Null when <paramref name="parserType"/> isn't an <see cref="IUpdatableHtmlParser"/>.</summary>
    public static GalleryUpdateSupport? For(Type? parserType)
    {
        if (parserType is null || !typeof(IUpdatableHtmlParser).IsAssignableFrom(parserType))
        {
            return null;
        }

        var defaultStrategy = GetStatic<GalleryUpdateStrategy>(parserType, nameof(IUpdatableHtmlParser.DefaultUpdateStrategy));
        var supported = GetStatic<GalleryUpdateStrategy[]>(parserType, nameof(IUpdatableHtmlParser.SupportedUpdateStrategies));
        return new GalleryUpdateSupport(defaultStrategy, [defaultStrategy, ..supported.Where(s => s != defaultStrategy).Distinct()]);
    }

    // FlattenHierarchy: the members may be declared on a shared base parser
    private static T GetStatic<T>(Type type, string name) =>
        (T)type.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.GetValue(null)!;

    public bool IsSupported(GalleryUpdateStrategy strategy) => Supported.Contains(strategy);

    /// <summary><paramref name="requested"/> if supported, otherwise <see cref="Default"/>.</summary>
    public GalleryUpdateStrategy Resolve(GalleryUpdateStrategy? requested) =>
        requested is { } strategy && IsSupported(strategy) ? strategy : Default;
}
