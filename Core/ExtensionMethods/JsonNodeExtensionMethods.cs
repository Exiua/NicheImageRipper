using System.Text.Json;
using System.Text.Json.Nodes;

namespace Core.ExtensionMethods;

public static class JsonNodeExtensionMethods
{
    public static bool IsArray(this JsonNode node)
    {
        return node.GetValueKind() == JsonValueKind.Array;
    }
}