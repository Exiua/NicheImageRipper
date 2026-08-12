using System.Collections.Frozen;
using System.Linq.Expressions;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.Managers;
using Serilog;

namespace NicheImageRipper.Core.SiteParsing;

using HtmlParserCtor = Func<WebDriver, ApiClientManager, Dictionary<string, string>, FilenameScheme, HtmlParser>;

public static class HtmlParserFactory
{
    private static readonly ILogger Logger = Log.ForContext(typeof(HtmlParserFactory));

    private static readonly Dictionary<Type, HtmlParserCtor> HtmlParserCtors = new();
    private static readonly Dictionary<string, HtmlParserCtor> Parsers;
    private static readonly Dictionary<string, Type> ParserTypesByName;

    /// <summary>
    ///     Every base URL (scheme+host+trailing slash) supported by any registered parser, derived from each
    ///     parser's <see cref="IHtmlParser.SupportedUrls"/>. Used by <c>UrlUtility.UrlCheck</c> instead of a
    ///     hardcoded list, so plugin-registered parsers are automatically recognized without touching core code.
    /// </summary>
    public static FrozenSet<string> SupportedUrls { get; }

    static HtmlParserFactory()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var types = assemblies.SelectMany(assembly => assembly.GetTypes());
        var parserTypes = types.Where(type =>
                                    !type.IsAbstract && typeof(HtmlParser).IsAssignableFrom(type) &&
                                    typeof(IHtmlParser).IsAssignableFrom(type))
                               .ToList();
        Parsers = new Dictionary<string, HtmlParserCtor>(StringComparer.OrdinalIgnoreCase);
        foreach (var parserType in parserTypes)
        {
            if (parserType.GetProperty(nameof(IHtmlParser.ParserName))?.GetValue(null) is not string primaryName)
            {
                throw new InvalidOperationException(
                    $"Parser type {parserType.Name} does not have a valid ParserName property.");
            }

            var additionalNames =
                parserType.GetProperty(nameof(IMultiSiteHtmlParser.AdditionalParserNames))
                         ?.GetValue(null) as string[] ?? [];
            var ctor = CreateHtmlParserFactory(parserType);
            Parsers[primaryName] = ctor;
            foreach (var additionalName in additionalNames)
            {
                Parsers[additionalName] = ctor;
            }
        }

        //SupportedUrls = BuildSupportedUrls(parserTypes);
        SupportedUrls = new HashSet<string>().ToFrozenSet();

        ParserTypesByName = parserTypes
                           .SelectMany(t =>
                            {
                                var primaryName =
                                    (string)t.GetProperty(nameof(IHtmlParser.ParserName))!.GetValue(null)!;
                                var additionalNames = typeof(IMultiSiteHtmlParser).IsAssignableFrom(t)
                                    ? (string[])t.GetProperty(nameof(IMultiSiteHtmlParser.AdditionalParserNames))!
                                                 .GetValue(null)!
                                    : [];
                                return new[] { primaryName }.Concat(additionalNames)
                                                            .Select(name => (Name: name, Type: t));
                            })
                           .ToDictionary(x => x.Name, x => x.Type, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Resolves a registered site name to its declaring parser Type, without constructing an instance.</summary>
    public static Type? ResolveType(string site) => ParserTypesByName.GetValueOrDefault(site);
    
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

        var lambda = Expression.Lambda<HtmlParserCtor>(
            cast, p1, p2, p3, p4);
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
        return !Parsers.TryGetValue(site, out var ctor)
            ? throw new RipperException($"Unsupported site: {site}")
            : ctor(driver, client, headers, scheme);
    }
}