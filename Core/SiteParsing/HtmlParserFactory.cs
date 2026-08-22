using System.Collections.Frozen;
using System.Linq.Expressions;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using NicheImageRipper.Core.Utility;
using Serilog;

namespace NicheImageRipper.Core.SiteParsing;

using HtmlParserCtor = Func<WebDriver, ApiClientManager, Dictionary<string, string>, FilenameScheme, HtmlParser>;
using ParameterizedHtmlParserCtor =
    Func<WebDriver, ApiClientManager, Dictionary<string, string>, FilenameScheme, ParameterizedHtmlParser>;

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

    public static IReadOnlyList<(string From, string To)> UrlReplacements { get; private set; } = [];
    public static FrozenSet<string> DelegatableDomains { get; private set; } = FrozenSet<string>.Empty;

    static HtmlParserFactory()
    {
        SiteModuleLoader.LoadModules();
        LinkInfoDiscovery.EnsureAllRegistered();
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
        UrlReplacements = BuildUrlReplacements(parserTypes);
        DelegatableDomains = BuildDelegatableDomains(parserTypes);
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

    private static HtmlParserCtor CreateHtmlParserCtor(Type type)
    {
        if (HtmlParserCtors.TryGetValue(type, out var existingCtor))
        {
            return existingCtor;
        }

        var ctor = GetStandardConstructor(type);
        var (p1, p2, p3, p4) = StandardParameters();

        var newExpr = Expression.New(ctor, p1, p2, p3, p4);
        var cast = Expression.Convert(newExpr, typeof(HtmlParser));
        var lambda = Expression.Lambda<HtmlParserCtor>(cast, p1, p2, p3, p4);
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
        var (p1, p2, p3, p4) = StandardParameters();

        var newExpr = Expression.New(ctor, p1, p2, p3, p4);
        var cast = Expression.Convert(newExpr, typeof(ParameterizedHtmlParser));
        var lambda = Expression.Lambda<ParameterizedHtmlParserCtor>(cast, p1, p2, p3, p4);
        var compiled = lambda.Compile();
        ParameterizedHtmlParserCtors[type] = compiled;
        return compiled;
    }

    private static System.Reflection.ConstructorInfo GetStandardConstructor(Type type)
    {
        var ctor = type.GetConstructor(
            [
                typeof(WebDriver), typeof(ApiClientManager), typeof(Dictionary<string, string>), typeof(FilenameScheme)
            ]
        );

        if (ctor is null)
        {
            throw new InvalidOperationException($"{type.Name} does not have the expected constructor.");
        }

        return ctor;
    }

    private static (ParameterExpression, ParameterExpression, ParameterExpression, ParameterExpression)
        StandardParameters()
    {
        return (
            Expression.Parameter(typeof(WebDriver), "driver"),
            Expression.Parameter(typeof(ApiClientManager), "clientManager"),
            Expression.Parameter(typeof(Dictionary<string, string>), "requestHeaders"),
            Expression.Parameter(typeof(FilenameScheme), "filenameScheme")
        );
    }

    /// <summary>Resolves and constructs the parser registered for <paramref name="site"/>.</summary>
    /// <exception cref="RipperException">No parser is registered for <paramref name="site"/>.</exception>
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
        ApiClientManager client,
        Dictionary<string, string> headers,
        FilenameScheme scheme)
    {
        return !_parameterizedParsers.TryGetValue(site, out var ctor)
            ? throw new RipperException(
                $"No delegatable (ParameterizedHtmlParser) parser is registered for site: {site}")
            : ctor(driver, client, headers, scheme);
    }
}