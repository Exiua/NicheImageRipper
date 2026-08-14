using System.Collections.Frozen;
using System.Linq.Expressions;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.Utility;
using Serilog;

namespace NicheImageRipper.Core.SiteParsing;

using HtmlParserCtor = Func<WebDriver, ApiClientManager, Dictionary<string, string>, FilenameScheme, HtmlParser>;

public static class HtmlParserFactory
{
    private static readonly ILogger Logger = Log.ForContext(typeof(HtmlParserFactory));

    private static readonly Dictionary<Type, HtmlParserCtor> HtmlParserCtors = new();

    private static Dictionary<string, HtmlParserCtor> _parsers = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, Type> _parserTypesByName = new(StringComparer.OrdinalIgnoreCase);
    private static FrozenSet<string> _supportedUrls = FrozenSet<string>.Empty;
    private static FrozenDictionary<string, int> _subdomainSignificantSuffixes = FrozenDictionary<string, int>.Empty;
    private static FrozenDictionary<string, string> _refererOverridesBySuffix = FrozenDictionary<string, string>.Empty;
    private static IReadOnlyList<(string From, string To)> _urlReplacements = [];

    /// <summary>
    ///     Every base URL (scheme+host+trailing slash) supported by any registered parser, derived from each
    ///     parser's <see cref="IHtmlParser.SupportedUrls"/>. Used by <c>UrlUtility.UrlCheck</c> instead of a
    ///     hardcoded list, so plugin-registered parsers are automatically recognized without touching core code.
    /// </summary>
    public static FrozenSet<string> SupportedUrls => _supportedUrls;

    public static FrozenDictionary<string, int> SubdomainSignificantSuffixes => _subdomainSignificantSuffixes;
    public static FrozenDictionary<string, string> RefererOverridesBySuffix => _refererOverridesBySuffix;
    public static IReadOnlyList<(string From, string To)> UrlReplacements => _urlReplacements;

    static HtmlParserFactory()
    {
        SiteModuleLoader.LoadModules();
        Rebuild();
    }

    /// <summary>
    ///     Re-scans loaded assemblies and rebuilds all parser registries. Call after
    ///     <see cref="SiteModuleLoader.LoadModules"/> picks up newly-dropped-in modules — not intended to be
    ///     called while a rip is in progress.
    /// </summary>
    public static void Rebuild()
    {
        var parserTypes = AppDomain.CurrentDomain
                                   .GetAssemblies()
                                   .SelectMany(assembly =>
                                    {
                                        try
                                        {
                                            return assembly.GetTypes();
                                        }
                                        catch
                                        {
                                            return [];
                                        }
                                    })
                                   .Where(type =>
                                        !type.IsAbstract &&
                                        typeof(HtmlParser).IsAssignableFrom(type) &&
                                        typeof(IHtmlParser).IsAssignableFrom(type))
                                   .ToList();

        _parsers = BuildParsers(parserTypes);
        _parserTypesByName = BuildParserTypesByName(parserTypes);
        _supportedUrls = BuildSupportedUrls(parserTypes);
        _subdomainSignificantSuffixes = BuildSuffixLookup<int>(
            parserTypes, typeof(ISubdomainSignificantHtmlParser),
            nameof(ISubdomainSignificantHtmlParser.SignificantDomainLabels));
        _refererOverridesBySuffix = BuildSuffixLookup<string>(
            parserTypes, typeof(IRefererOverrideHtmlParser), nameof(IRefererOverrideHtmlParser.RefererOverride));
        _urlReplacements = BuildUrlReplacements(parserTypes);
    }

    private static Dictionary<string, HtmlParserCtor> BuildParsers(IEnumerable<Type> parserTypes)
    {
        var parsers = new Dictionary<string, HtmlParserCtor>(StringComparer.OrdinalIgnoreCase);

        foreach (var parserType in parserTypes)
        {
            if (parserType.GetProperty(nameof(IHtmlParser.ParserName))?.GetValue(null) is not string primaryName)
            {
                throw new InvalidOperationException(
                    $"Parser type {parserType.Name} does not have a valid ParserName property.");
            }

            var additionalNames = typeof(IMultiSiteHtmlParser).IsAssignableFrom(parserType)
                ? (string[])parserType.GetProperty(nameof(IMultiSiteHtmlParser.AdditionalParserNames))!.GetValue(null)!
                : [];

            var ctor = CreateHtmlParserFactory(parserType);
            parsers[primaryName] = ctor;
            foreach (var additionalName in additionalNames)
            {
                parsers[additionalName] = ctor;
            }
        }

        return parsers;
    }

    private static Dictionary<string, Type> BuildParserTypesByName(IEnumerable<Type> parserTypes)
    {
        return parserTypes
              .SelectMany(t =>
               {
                   var primaryName = (string)t.GetProperty(nameof(IHtmlParser.ParserName))!.GetValue(null)!;
                   var additionalNames = typeof(IMultiSiteHtmlParser).IsAssignableFrom(t)
                       ? (string[])t.GetProperty(nameof(IMultiSiteHtmlParser.AdditionalParserNames))!.GetValue(null)!
                       : [];
                   return new[] { primaryName }.Concat(additionalNames).Select(name => (Name: name, Type: t));
               })
              .ToDictionary(x => x.Name, x => x.Type, StringComparer.OrdinalIgnoreCase);
    }

    private static FrozenSet<string> BuildSupportedUrls(IEnumerable<Type> parserTypes)
    {
        var owners = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in parserTypes)
        {
            var supportedUrls = (string[])type.GetProperty(nameof(IHtmlParser.SupportedUrls))!.GetValue(null)!;
            foreach (var url in supportedUrls)
            {
                if (owners.TryGetValue(url, out var existingOwner) && existingOwner != type)
                {
                    Logger.Warning(
                        "Supported URL {Url} is claimed by both {ExistingParser} (existing) and {NewParser} (newer); newer will take precedence",
                        url, existingOwner.Name, type.Name);
                }

                owners[url] = type; // last wins
            }
        }

        return owners.Keys.ToFrozenSet();
    }

    /// <summary>
    ///     Builds a domain-suffix -> value lookup from every parser
    ///     reading <paramref name="propertyName"/> off each and keying by every host in that parser's
    ///     <see cref="IHtmlParser.SupportedUrls"/>. On a suffix collision, the later-enumerated parser wins and
    ///     a warning is logged.
    /// </summary>
    private static FrozenDictionary<string, TValue> BuildSuffixLookup<TValue>(
        IEnumerable<Type> parserTypes, Type markerInterface, string propertyName)
    {
        var owners = new Dictionary<string, (Type Owner, TValue Value)>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in parserTypes.Where(markerInterface.IsAssignableFrom))
        {
            var value = (TValue)type.GetProperty(propertyName)!.GetValue(null)!;
            var urls = (string[])type.GetProperty(nameof(IHtmlParser.SupportedUrls))!.GetValue(null)!;

            foreach (var suffix in urls.Select(u => new Uri(u).Host).Distinct())
            {
                if (owners.TryGetValue(suffix, out var existing) && existing.Owner != type)
                {
                    Logger.Warning(
                        "Domain suffix {Suffix} claimed by both {Existing} (existing) and {New} (new); newer will take precedence",
                        suffix, existing.Owner.Name, type.Name);
                }

                owners[suffix] = (type, value);
            }
        }

        return owners.ToFrozenDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
    }

    private static List<(string From, string To)> BuildUrlReplacements(IEnumerable<Type> parserTypes)
    {
        return parserTypes
              .Where(t => typeof(IUrlNormalizingHtmlParser).IsAssignableFrom(t))
              .SelectMany(t =>
                   (ValueTuple<string, string>[])t.GetProperty(nameof(IUrlNormalizingHtmlParser.UrlReplacements))!
                                                  .GetValue(null)!)
              .ToList();
    }

    /// <summary>Resolves a registered site name to its declaring parser Type, without constructing an instance.</summary>
    public static Type? ResolveType(string site) => _parserTypesByName.GetValueOrDefault(site);

    private static HtmlParserCtor CreateHtmlParserFactory(Type type)
    {
        if (HtmlParserCtors.TryGetValue(type, out var existingCtor))
        {
            return existingCtor;
        }

        var ctor = type.GetConstructor(
            [
                typeof(WebDriver), typeof(ApiClientManager), typeof(Dictionary<string, string>), typeof(FilenameScheme)
            ]
        );

        if (ctor is null)
        {
            throw new InvalidOperationException($"{type.Name} does not have the expected constructor.");
        }

        var p1 = Expression.Parameter(typeof(WebDriver), "driver");
        var p2 = Expression.Parameter(typeof(ApiClientManager), "clientManager");
        var p3 = Expression.Parameter(typeof(Dictionary<string, string>), "requestHeaders");
        var p4 = Expression.Parameter(typeof(FilenameScheme), "filenameScheme");

        var newExpr = Expression.New(ctor, p1, p2, p3, p4);
        var cast = Expression.Convert(newExpr, typeof(HtmlParser));
        var lambda = Expression.Lambda<HtmlParserCtor>(cast, p1, p2, p3, p4);
        var compiled = lambda.Compile();
        HtmlParserCtors[type] = compiled;
        return compiled;
    }

    public static HtmlParser Create(
        string site,
        WebDriver driver,
        ApiClientManager client,
        Dictionary<string, string> headers,
        FilenameScheme scheme)
    {
        return !_parsers.TryGetValue(site, out var ctor)
            ? throw new RipperException($"Unsupported site: {site}")
            : ctor(driver, client, headers, scheme);
    }
}