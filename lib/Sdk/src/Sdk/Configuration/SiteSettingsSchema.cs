using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Sdk.Configuration;

/// <summary>How a <see cref="SiteSettingField"/> is edited and stored in the custom settings JSON.</summary>
public enum SiteSettingKind
{
    /// <summary>A JSON string.</summary>
    Text,
    /// <summary>A JSON string, masked in the editor.</summary>
    Secret,
    /// <summary>A JSON boolean.</summary>
    Toggle,
    /// <summary>A JSON integer.</summary>
    Integer,
    /// <summary>A JSON number.</summary>
    Number,
    /// <summary>A JSON string, one of <see cref="SiteSettingField.Choices"/> (an enum, stored by name).</summary>
    Choice,
    /// <summary>A JSON array of strings.</summary>
    TextList,
    /// <summary>A JSON array of objects, each made of <see cref="SiteSettingField.ItemFields"/>.</summary>
    ObjectList,
}

/// <summary>A <see cref="SiteSettingKind.Choice"/> option: an enum member's name and numeric value.</summary>
public sealed record SiteSettingChoice(string Name, long Value);

/// <summary>One editable setting, describing a property of a site's custom settings class.</summary>
/// <param name="Key">The JSON property name.</param>
/// <param name="DefaultValue">The property's value on a new settings object; null when it has none.</param>
/// <param name="Choices">For <see cref="SiteSettingKind.Choice"/>.</param>
/// <param name="ItemFields">For <see cref="SiteSettingKind.ObjectList"/>: the fields of one entry.</param>
/// <param name="ItemLabel">For <see cref="SiteSettingKind.ObjectList"/>: what one entry is called.</param>
public sealed record SiteSettingField(
    string Key,
    string Label,
    string? Description,
    SiteSettingKind Kind,
    JsonElement? DefaultValue = null,
    IReadOnlyList<SiteSettingChoice>? Choices = null,
    IReadOnlyList<SiteSettingField>? ItemFields = null,
    string? ItemLabel = null);

/// <summary>
///     A serializable description of a site's custom settings, built from its
///     <see cref="ICustomSettingsHtmlParser.CustomSettingsType"/> so a GUI (possibly on another machine than the site
///     modules) can render an editor for them.
/// </summary>
public sealed record SiteSettingsSchema(IReadOnlyList<SiteSettingField> Fields)
{
    /// <summary>Null when <paramref name="parserType"/> isn't an <see cref="ICustomSettingsHtmlParser"/>.</summary>
    public static SiteSettingsSchema? For(Type? parserType)
    {
        if (parserType is null || !typeof(ICustomSettingsHtmlParser).IsAssignableFrom(parserType))
        {
            return null;
        }

        // FlattenHierarchy: the member may be declared on a shared base parser
        var settingsType = (Type)parserType.GetProperty(nameof(ICustomSettingsHtmlParser.CustomSettingsType),
                                                BindingFlags.Public | BindingFlags.Static |
                                                BindingFlags.FlattenHierarchy)!
                                           .GetValue(null)!;
        return FromSettingsType(settingsType);
    }

    /// <exception cref="InvalidOperationException">A <see cref="SiteSettingAttribute"/> property has an unsupported
    /// type, or the class has no parameterless constructor.</exception>
    public static SiteSettingsSchema FromSettingsType(Type settingsType) => new(GetFields(settingsType));

    private static List<SiteSettingField> GetFields(Type settingsType)
    {
        var defaults = Activator.CreateInstance(settingsType)
                       ?? throw new InvalidOperationException($"{settingsType.Name} has no parameterless constructor");
        var fields = new List<SiteSettingField>();
        foreach (var property in settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<SiteSettingAttribute>() is not { } setting
                || property.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always })
            {
                continue;
            }

            fields.Add(CreateField(property, setting, property.GetValue(defaults)));
        }

        return fields;
    }

    private static SiteSettingField CreateField(PropertyInfo property, SiteSettingAttribute setting, object? defaultValue)
    {
        var key = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var defaultJson = defaultValue is null
            ? (JsonElement?)null
            : JsonSerializer.SerializeToElement(defaultValue, property.PropertyType, SiteSettings.JsonOptions);
        var field = new SiteSettingField(key, setting.Label, setting.Description, SiteSettingKind.Text, defaultJson);

        if (type == typeof(string))
        {
            return field with { Kind = setting.Secret ? SiteSettingKind.Secret : SiteSettingKind.Text };
        }

        if (type == typeof(bool))
        {
            return field with { Kind = SiteSettingKind.Toggle };
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            return field with { Kind = SiteSettingKind.Integer };
        }

        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            return field with { Kind = SiteSettingKind.Number };
        }

        if (type.IsEnum)
        {
            var choices = Enum.GetNames(type)
                              .Select(name => new SiteSettingChoice(name,
                                   Convert.ToInt64(Enum.Parse(type, name))))
                              .ToList();
            return field with { Kind = SiteSettingKind.Choice, Choices = choices };
        }

        if (GetElementType(type) is { } elementType)
        {
            if (elementType == typeof(string))
            {
                return field with { Kind = SiteSettingKind.TextList };
            }

            if (elementType.IsClass)
            {
                return field with
                {
                    Kind = SiteSettingKind.ObjectList,
                    ItemFields = GetFields(elementType),
                    ItemLabel = setting.ItemLabel,
                };
            }
        }

        throw new InvalidOperationException(
            $"{property.DeclaringType?.Name}.{property.Name}: site settings of type {property.PropertyType.Name} aren't supported");
    }

    private static Type? GetElementType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        return type.GetInterfaces()
                   .Append(type)
                   .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                   ?.GetGenericArguments()[0];
    }
}
