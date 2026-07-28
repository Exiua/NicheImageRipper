using System.Linq.Expressions;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.Managers;

namespace NicheImageRipper.Core.SiteParsing;

using HtmlParserCtor = Func<WebDriver, ApiClientManager, Dictionary<string, string>, FilenameScheme, HtmlParser>;

public static class HtmlParserFactory
{
    private static readonly Dictionary<Type, HtmlParserCtor> HtmlParserCtors = new();
    private static readonly Dictionary<string, HtmlParserCtor> Parsers;

    static HtmlParserFactory()
    {
        Parsers = AppDomain.CurrentDomain
                           .GetAssemblies()
                           .SelectMany(a =>
                            {
                                try
                                {
                                    return a.GetTypes();
                                }
                                catch
                                {
                                    return [];
                                }
                            })
                           .Where(t => !t.IsAbstract && typeof(HtmlParser).IsAssignableFrom(t) &&
                                       typeof(IHtmlParser).IsAssignableFrom(t))
                           .SelectMany(t =>
                            {
                                var primaryName =
                                    (string)t.GetProperty(nameof(IHtmlParser.ParserName))!.GetValue(null)!;
                                var additionalNames =
                                    (string[])t.GetProperty(nameof(IHtmlParser.AdditionalParserNames))!.GetValue(null)!;
                                var ctor = CreateHtmlParserFactory(t);
                                return new[] { primaryName }.Concat(additionalNames)
                                                            .Select(name => (Name: name, Ctor: ctor));
                            })
                           .ToDictionary(x => x.Name, x => x.Ctor, StringComparer.OrdinalIgnoreCase);
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