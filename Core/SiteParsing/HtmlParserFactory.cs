using System.Collections.Frozen;
using System.Linq.Expressions;
using NicheImageRipper.Core.ModuleLoading;
using NicheImageRipper.Core.Utility;
using NicheImageRipper.Sdk.Driver;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.SiteParsing;
using Serilog;

namespace NicheImageRipper.Core.SiteParsing;

using HtmlParserCtor = Func<WebDriver, Dictionary<string, string>, FilenameScheme, HtmlParser>;
using ParameterizedHtmlParserCtor =
    Func<WebDriver, Dictionary<string, string>, FilenameScheme, ParameterizedHtmlParser>;

public static class HtmlParserFactory
{
    private static readonly ILogger Logger = Log.ForContext(typeof(HtmlParserFactory));

    private static readonly Dictionary<Type, HtmlParserCtor> HtmlParserCtors = new();
    private static readonly Dictionary<Type, ParameterizedHtmlParserCtor> ParameterizedHtmlParserCtors = new();

    private static Dictionary<string, HtmlParserCtor> _parsers = new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, ParameterizedHtmlParserCtor> _parameterizedParsers =
        new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, Type> _parserTypesByName = new(StringComparer.OrdinalIgnoreCase);

    public static FrozenSet<string> SupportedUrls { get; private set; } = FrozenSet<string>.Empty;

    public static FrozenDictionary<string, int> SubdomainSignificantSuffixes { get; private set; } =
        FrozenDictionary<string, int>.Empty;

    public static FrozenDictionary<string, string> RefererOverridesBySuffix { get; private set; } =
        FrozenDictionary<string, string>.Empty;

    public static FrozenSet<string> DelegatableDomains { get; private set; } = FrozenSet<string>.Empty;

    public static IReadOnlyList<(string Pattern, UrlMatchKind Kind, Func<string, string> Normalize)> UrlNormalizers
    {
        get;
        private set;
    } = [];

    static HtmlParserFactory()
    {
        SiteModuleLoader.LoadModules();
        LinkInfoDiscovery.EnsureAllRegistered();
        HtmlParser.ParserResolver = new CoreParserResolver();
        Rebuild();
    }

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
        _parameterizedParsers = BuildParameterizedParsers(parserTypes);
        _parserTypesByName = BuildParserTypesByName(parserTypes);
        SupportedUrls = BuildSupportedUrls(parserTypes);
        SubdomainSignificantSuffixes = BuildSuffixLookup<int>(
            parserTypes, typeof(ISubdomainSignificantHtmlParser),
            nameof(ISubdomainSignificantHtmlParser.SignificantDomainLabels));
        RefererOverridesBySuffix = BuildSuffixLookup<string>(
            parserTypes, typeof(IRefererOverrideHtmlParser), nameof(IRefererOverrideHtmlParser.RefererOverride));
        DelegatableDomains = BuildDelegatableDomains(parserTypes);
        UrlNormalizers = BuildUrlNormalizers(parserTypes);
    }

    private static List<(string Pattern, UrlMatchKind Kind, Func<string, string> Normalize)> BuildUrlNormalizers(
        IEnumerable<Type> parserTypes)
    {
        var normalizers = new List<(string, UrlMatchKind, Func<string, string>)>();
        var owners = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in parserTypes.Where(t => typeof(INormalizingHtmlParser).IsAssignableFrom(t)))
        {
            var method = type.GetMethod(nameof(INormalizingHtmlParser.NormalizeUrl),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
            var normalize = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>), method);
            var patterns = (IReadOnlyList<(string Pattern, UrlMatchKind Kind)>)
                type.GetProperty(nameof(INormalizingHtmlParser.NormalizationPatterns))!.GetValue(null)!;

            foreach (var (pattern, kind) in patterns)
            {
                var key = $"{kind}:{pattern}";
                if (owners.TryGetValue(key, out var existingOwner) && existingOwner != type)
                {
                    Logger.Warning(
                        "Normalization pattern {Kind} {Pattern} claimed by both {Existing} (existing) and {New} (new); newer will take precedence",
                        kind, pattern, existingOwner.Name, type.Name);
                }

                owners[key] = type;
                normalizers.Add((pattern, kind, normalize));
            }
        }

        return normalizers;
    }

    private static FrozenSet<string> BuildDelegatableDomains(IEnumerable<Type> parserTypes)
    {
        return parserTypes
              .Where(t => typeof(ParameterizedHtmlParser).IsAssignableFrom(t))
              .SelectMany(t => (string[])t.GetProperty(nameof(IHtmlParser.SupportedUrls))!.GetValue(null)!)
              .Select(url => new Uri(url).Host)
              .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
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

            var ctor = CreateHtmlParserCtor(parserType);
            parsers[primaryName] = ctor;
            foreach (var additionalName in additionalNames)
            {
                parsers[additionalName] = ctor;
            }
        }

        return parsers;
    }

    /// <summary>
    ///     Builds the same name -> constructor mapping as <see cref="BuildParsers"/>, restricted to parser
    ///     types that derive from <see cref="ParameterizedHtmlParser"/>. Used by <see cref="CreateParameterized"/>
    ///     so callers wanting to delegate to another site's parser (e.g. a Mega/Drive link embedded on another
    ///     site's page) get a typed <see cref="ParameterizedHtmlParser"/> back without an unchecked cast.
    /// </summary>
    private static Dictionary<string, ParameterizedHtmlParserCtor> BuildParameterizedParsers(
        IEnumerable<Type> parserTypes)
    {
        var parsers = new Dictionary<string, ParameterizedHtmlParserCtor>(StringComparer.OrdinalIgnoreCase);

        foreach (var parserType in parserTypes.Where(t => typeof(ParameterizedHtmlParser).IsAssignableFrom(t)))
        {
            var primaryName = (string)parserType.GetProperty(nameof(IHtmlParser.ParserName))!.GetValue(null)!;
            var additionalNames = typeof(IMultiSiteHtmlParser).IsAssignableFrom(parserType)
                ? (string[])parserType.GetProperty(nameof(IMultiSiteHtmlParser.AdditionalParserNames))!.GetValue(null)!
                : [];

            var ctor = CreateParameterizedHtmlParserCtor(parserType);
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

                owners[url] = type;
            }
        }

        return owners.Keys.ToFrozenSet();
    }

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

    /// <summary>Resolves a registered site name to its declaring parser Type, without constructing an instance.</summary>
    public static Type? ResolveType(string site) => _parserTypesByName.GetValueOrDefault(site);

    private static HtmlParserCtor CreateHtmlParserCtor(Type type)
    {
        if (HtmlParserCtors.TryGetValue(type, out var existingCtor))
        {
            return existingCtor;
        }

        var ctor = GetStandardConstructor(type);
        var (p1, p2, p3) = StandardParameters();

        var newExpr = Expression.New(ctor, p1, p2, p3);
        var cast = Expression.Convert(newExpr, typeof(HtmlParser));
        var lambda = Expression.Lambda<HtmlParserCtor>(cast, p1, p2, p3);
        var compiled = lambda.Compile();
        HtmlParserCtors[type] = compiled;
        return compiled;
    }

    private static ParameterizedHtmlParserCtor CreateParameterizedHtmlParserCtor(Type type)
    {
        if (ParameterizedHtmlParserCtors.TryGetValue(type, out var existingCtor))
        {
            return existingCtor;
        }

        var ctor = GetStandardConstructor(type);
        var (p1, p2, p3) = StandardParameters();

        var newExpr = Expression.New(ctor, p1, p2, p3);
        var cast = Expression.Convert(newExpr, typeof(ParameterizedHtmlParser));
        var lambda = Expression.Lambda<ParameterizedHtmlParserCtor>(cast, p1, p2, p3);
        var compiled = lambda.Compile();
        ParameterizedHtmlParserCtors[type] = compiled;
        return compiled;
    }

    private static System.Reflection.ConstructorInfo GetStandardConstructor(Type type)
    {
        var ctor = type.GetConstructor(
            [
                typeof(WebDriver), typeof(Dictionary<string, string>), typeof(FilenameScheme)
            ]
        );

        if (ctor is null)
        {
            throw new InvalidOperationException($"{type.Name} does not have the expected constructor.");
        }

        return ctor;
    }

    private static (ParameterExpression, ParameterExpression, ParameterExpression)
        StandardParameters()
    {
        return (
            Expression.Parameter(typeof(WebDriver), "driver"),
            Expression.Parameter(typeof(Dictionary<string, string>), "requestHeaders"),
            Expression.Parameter(typeof(FilenameScheme), "filenameScheme")
        );
    }

    /// <summary>Resolves and constructs the parser registered for <paramref name="site"/>.</summary>
    /// <exception cref="RipperException">No parser is registered for <paramref name="site"/>.</exception>
    public static HtmlParser Create(
        string site,
        WebDriver driver,
        Dictionary<string, string> headers,
        FilenameScheme scheme)
    {
        return !_parsers.TryGetValue(site, out var ctor)
            ? throw new RipperException($"Unsupported site: {site}")
            : ctor(driver, headers, scheme);
    }

    /// <summary>
    ///     Resolves and constructs the parser registered for <paramref name="site"/>, typed as
    ///     <see cref="ParameterizedHtmlParser"/> so it can be invoked with an explicit URL for delegation
    ///     (e.g. a parser encountering a Mega/Drive link embedded in a page it's scraping). Only succeeds for
    ///     sites whose registered parser actually derives from <see cref="ParameterizedHtmlParser"/> — check
    ///     <see cref="DelegatableDomains"/> first if you don't already know the site supports delegation.
    /// </summary>
    /// <exception cref="RipperException">
    ///     No parser is registered for <paramref name="site"/>, or its parser does not support delegation.
    /// </exception>
    public static ParameterizedHtmlParser CreateParameterized(
        string site,
        WebDriver driver,
        Dictionary<string, string> headers,
        FilenameScheme scheme)
    {
        return !_parameterizedParsers.TryGetValue(site, out var ctor)
            ? throw new ParameterizedParserNotFound(site)
            : ctor(driver, headers, scheme);
    }
}