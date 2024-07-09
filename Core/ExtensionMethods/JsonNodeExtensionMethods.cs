using System.Text.Json;
using System.Text.Json.Nodes;

namespace Core.ExtensionMethods;

public static class JsonNodeExtensionMethods
{
    public static bool IsArray(this JsonNode node)
    {
        return node.GetValueKind() == JsonValueKind.Array;
    }
    
    public static bool IsNull(this JsonNode? node)
    {
        if (node == null)
        {
            return true;
        }
        
        return node.GetValueKind() == JsonValueKind.Null;
    }
}