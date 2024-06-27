﻿using System.Runtime.CompilerServices;
using Core.Exceptions;
using HtmlAgilityPack;

namespace Core.ExtensionMethods;

public static class HtmlAgilityPackExtensionMethods
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<string> GetHrefs(this HtmlNodeCollection nodes)
    {
        return nodes
            .SelectWhere(node => 
                node.GetAttributeValue("href", string.Empty), link => link != string.Empty)
            .ToList();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<string> GetSrcs(this HtmlNodeCollection nodes)
    {
        return nodes
            .Select(node => node.GetSrc())
            .ToList();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<string> GetSrcs(this IEnumerable<HtmlNode> nodes)
    {
        return nodes
              .Select(node => node.GetSrc())
              .ToList();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetSrc(this HtmlNode node)
    {
        return node.GetAttributeValue("src");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetHref(this HtmlNode node)
    {
        return node.GetAttributeValue("href");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetAttributeValue(this HtmlNode node, string attributeName)
    {
        var attribute = node.GetAttributeValue(attributeName, string.Empty);
        if (string.IsNullOrEmpty(attribute))
        {
            throw new AttributeNotFoundException($"No {attributeName} attribute found");
        }
        
        return attribute;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetNullableSrc(this HtmlNode node)
    {
        return node.GetNullableAttributeValue("src");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? GetNullableAttributeValue(this HtmlNode node, string attributeName)
    {
        return node.GetAttributeValue(attributeName, null);
    }
}