using System.Linq.Expressions;
using Core.Driver;
using Core.Enums;
using Core.Managers;

namespace Core.SiteParsing;

using HtmlParserCtor = Func<WebDriver, ApiClientManager, Dictionary<string, string>, FilenameScheme, HtmlParser>;

public class HtmlParserFactoryBuilder
{
    private static readonly Dictionary<Type, HtmlParserCtor> HtmlParserCtors = new();
    
    public static HtmlParserCtor CreateHtmlParserFactory<T>() where T : HtmlParser
    {
        var type = typeof(T);
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
}