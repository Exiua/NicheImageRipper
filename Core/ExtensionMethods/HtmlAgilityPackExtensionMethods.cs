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
            .SelectWhere(node => 
                node.GetAttributeValue("src", string.Empty), link => link != string.Empty)
            .ToList();
    }
    
    public static List<string> GetSrcs(this IEnumerable<HtmlNode> nodes)
    {
        return nodes
              .SelectWhere(node => 
                   node.GetAttributeValue("src", string.Empty), link => link != string.Empty)
              .ToList();
    }
}