using Common.Exceptions;
using HtmlAgilityPack;

namespace Common.ExtensionMethods;

public static class HtmlAgilityPackExtensionMethods
{
    public static List<string> GetHrefs(this HtmlNodeCollection nodes)
    {
        return nodes
              .Select(node => node.GetHref())
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
        // Caution: empty string will be treated as null
        var value = node.GetAttributeValue(attributeName,"");
        return string.IsNullOrEmpty(value) ? null : value;
    }
    
    public static HtmlNode SelectSingleNodeOrThrow(this HtmlNode node, string xpath)
    {
        var selectedNode = node.SelectSingleNode(xpath) ?? throw new ElementNotFoundException(xpath);
        return selectedNode;
    }
    
    public static HtmlNodeCollection SelectNodesOrThrow(this HtmlNode node, string xpath)
    {
        var selectedNodes = node.SelectNodes(xpath) ?? throw new ElementNotFoundException(xpath);
        return selectedNodes;
    }

    public static HtmlNodeCollection SelectNodesSafe(this HtmlNode node, string xpath)
    {
        return node.SelectNodes(xpath) ?? new HtmlNodeCollection(node);
    }
    
    public static string GetVideoSrc(this HtmlNode node)
    {
        var src = node.GetNullableSrc();
        if (src is not null)
        {
            return src;
        }
        
        var source = node.SelectSingleNode("source");
        if (source is null)
        {
            throw new AttributeNotFoundException("No video source found");
        }
        
        return source.GetSrc();
    }
}