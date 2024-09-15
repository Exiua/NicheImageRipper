using System.Runtime.CompilerServices;
using Core.Exceptions;
using HtmlAgilityPack;

namespace Core.ExtensionMethods;

public static class HtmlAgilityPackExtensionMethods
{
    public static List<string> GetHrefs(this HtmlNodeCollection nodes)
    {
        return nodes
            .SelectWhere(node => 
                node.GetAttributeValue("href", string.Empty), link => link != string.Empty)
            .ToList();
    }
    
    public static List<string> GetSrcs(this HtmlNodeCollection nodes)
    {
        return nodes
            .Select(node => node.GetSrc())
            .ToList();
    }
    
    public static List<string> GetSrcs(this IEnumerable<HtmlNode> nodes)
    {
        return nodes
              .Select(node => node.GetSrc())
              .ToList();
    }

    public static string GetSrc(this HtmlNode node)
    {
        return node.GetAttributeValue("src");
    }
    
    public static string GetHref(this HtmlNode node)
    {
        return node.GetAttributeValue("href");
    }
    
    public static string GetAttributeValue(this HtmlNode node, string attributeName)
    {
        var attribute = node.GetAttributeValue(attributeName, string.Empty);
        if (string.IsNullOrEmpty(attribute))
        {
            throw new AttributeNotFoundException($"No {attributeName} attribute found");
        }
        
        return attribute;
    }
    
    public static string? GetNullableSrc(this HtmlNode node)
    {
        return node.GetNullableAttributeValue("src");
    }
    
    public static string? GetNullableHref(this HtmlNode node)
    {
        return node.GetNullableAttributeValue("href");
    }
    
    public static string? GetNullableAttributeValue(this HtmlNode node, string attributeName)
    {
        return node.GetAttributeValue(attributeName, null);
    }

    public static HtmlNodeCollection SelectNodesSafe(this HtmlNode node, string xpath)
    {
        return node.SelectNodes(xpath) ?? new HtmlNodeCollection(node);
    }
}