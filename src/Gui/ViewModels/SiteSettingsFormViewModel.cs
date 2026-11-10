using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Text.Json;
using System.Text.Json.Nodes;
using NicheImageRipper.Sdk.Configuration;
using ReactiveUI;

namespace NicheImageRipper.Gui.ViewModels;

/// <summary>
///     An editor for a JSON object described by <see cref="SiteSettingField"/>s: a site's custom settings, or one entry
///     of an <see cref="SiteSettingKind.ObjectList"/>. Properties the fields don't describe are kept as they are.
/// </summary>
public class SiteSettingsFormViewModel : ViewModelBase
{
    private readonly JsonObject _unknownProperties = new();

    public IReadOnlyList<SiteSettingFieldViewModel> Fields { get; }

    /// <param name="value">The current settings; null starts from the fields' defaults.</param>
    public SiteSettingsFormViewModel(IReadOnlyList<SiteSettingField> fields, JsonObject? value)
    {
        var keys = new HashSet<string>(fields.Select(f => f.Key), StringComparer.OrdinalIgnoreCase);
        foreach (var (key, node) in value?.AsEnumerable() ?? [])
        {
            if (!keys.Contains(key))
            {
                _unknownProperties[key] = node?.DeepClone();
            }
        }

        Fields = fields.Select(f => SiteSettingFieldViewModel.Create(f, GetValue(value, f))).ToList();
    }

    // Settings are read case-insensitively, so match keys the same way
    private static JsonNode? GetValue(JsonObject? value, SiteSettingField field)
    {
        if (value is null)
        {
            return ToNode(field.DefaultValue);
        }

        foreach (var (key, node) in value)
        {
            if (string.Equals(key, field.Key, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
        }

        return ToNode(field.DefaultValue);
    }

    private static JsonNode? ToNode(JsonElement? element) =>
        element is { } json ? JsonSerializer.SerializeToNode(json) : null;

    public bool TryBuild(out JsonObject result, out string? error)
    {
        result = (JsonObject)_unknownProperties.DeepClone();
        foreach (var field in Fields)
        {
            if (!field.TryBuild(out var node, out error))
            {
                error = $"{field.Label}: {error}";
                return false;
            }

            if (node is not null)
            {
                result[field.Key] = node;
            }
        }

        error = null;
        return true;
    }
}

public abstract class SiteSettingFieldViewModel(SiteSettingField setting) : ViewModelBase
{
    public string Key => setting.Key;
    public string Label => setting.Label;
    public string? Description => setting.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(setting.Description);

    /// <param name="node">The JSON value, or null to leave the setting out (so the site uses its default).</param>
    public abstract bool TryBuild(out JsonNode? node, out string? error);

    public static SiteSettingFieldViewModel Create(SiteSettingField field, JsonNode? value) => field.Kind switch
    {
        SiteSettingKind.Text or SiteSettingKind.Secret => new TextSettingViewModel(field, value),
        SiteSettingKind.Toggle => new ToggleSettingViewModel(field, value),
        SiteSettingKind.Integer or SiteSettingKind.Number => new NumberSettingViewModel(field, value),
        SiteSettingKind.Choice => new ChoiceSettingViewModel(field, value),
        SiteSettingKind.TextList => new TextListSettingViewModel(field, value),
        SiteSettingKind.ObjectList => new ObjectListSettingViewModel(field, value),
        _ => throw new ArgumentOutOfRangeException(nameof(field), field.Kind, "Unknown site setting kind"),
    };

    /// <summary>The value as text: a JSON string's contents, or any other JSON as written.</summary>
    protected static string AsText(JsonNode? value) => value switch
    {
        null => "",
        JsonValue v when v.TryGetValue<string>(out var text) => text,
        _ => value.ToJsonString(),
    };
}

public class TextSettingViewModel : SiteSettingFieldViewModel
{
    public bool IsSecret { get; }

    public string Text
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool RevealSecret
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(MaskChar));
        }
    }

    public char MaskChar => IsSecret && !RevealSecret ? '•' : '\0';

    public TextSettingViewModel(SiteSettingField field, JsonNode? value) : base(field)
    {
        IsSecret = field.Kind == SiteSettingKind.Secret;
        Text = AsText(value);
    }

    public override bool TryBuild(out JsonNode? node, out string? error)
    {
        node = JsonValue.Create(Text);
        error = null;
        return true;
    }
}

public class ToggleSettingViewModel : SiteSettingFieldViewModel
{
    public bool IsChecked
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ToggleSettingViewModel(SiteSettingField field, JsonNode? value) : base(field)
    {
        IsChecked = value is JsonValue v && v.TryGetValue<bool>(out var isChecked) && isChecked;
    }

    public override bool TryBuild(out JsonNode? node, out string? error)
    {
        node = JsonValue.Create(IsChecked);
        error = null;
        return true;
    }
}

public class NumberSettingViewModel : SiteSettingFieldViewModel
{
    private readonly bool _isInteger;

    public string Text
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Placeholder => _isInteger ? "Whole number" : "Number";

    public NumberSettingViewModel(SiteSettingField field, JsonNode? value) : base(field)
    {
        _isInteger = field.Kind == SiteSettingKind.Integer;
        Text = AsText(value);
    }

    public override bool TryBuild(out JsonNode? node, out string? error)
    {
        node = null;
        error = null;
        var text = Text.Trim();
        if (text.Length == 0)
        {
            return true;
        }

        if (_isInteger && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            node = JsonValue.Create(integer);
            return true;
        }

        if (!_isInteger && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            node = JsonValue.Create(number);
            return true;
        }

        error = _isInteger ? "must be a whole number" : "must be a number";
        return false;
    }
}

public class ChoiceSettingViewModel : SiteSettingFieldViewModel
{
    public IReadOnlyList<string> Choices { get; }

    // ComboBoxes push a null SelectedItem back while their items are being (re)bound; ignore it rather than
    // losing the selection.
    public string Selected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value ?? field);
    }

    public ChoiceSettingViewModel(SiteSettingField field, JsonNode? value) : base(field)
    {
        var choices = field.Choices ?? [];
        Choices = choices.Select(c => c.Name).ToList();
        // Enums may also have been written by hand as numbers
        Selected = value switch
        {
            JsonValue v when v.TryGetValue<string>(out var name) =>
                Choices.FirstOrDefault(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)),
            JsonValue v when v.TryGetValue<long>(out var number) =>
                choices.FirstOrDefault(c => c.Value == number)?.Name,
            _ => null,
        } ?? Choices.FirstOrDefault() ?? "";
    }

    public override bool TryBuild(out JsonNode? node, out string? error)
    {
        node = Selected.Length == 0 ? null : JsonValue.Create(Selected);
        error = null;
        return true;
    }
}

public class TextListSettingViewModel : SiteSettingFieldViewModel
{
    /// <summary>One value per line.</summary>
    public string Text
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public TextListSettingViewModel(SiteSettingField field, JsonNode? value) : base(field)
    {
        var lines = value is JsonArray array ? array.Select(AsText) : [];
        Text = string.Join(Environment.NewLine, lines);
    }

    public override bool TryBuild(out JsonNode? node, out string? error)
    {
        var lines = Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        node = new JsonArray(lines.Select(line => (JsonNode?)JsonValue.Create(line)).ToArray());
        error = null;
        return true;
    }
}

public class ObjectListSettingViewModel : SiteSettingFieldViewModel
{
    private readonly IReadOnlyList<SiteSettingField> _itemFields;
    private readonly string _itemLabel;

    public ObservableCollection<SiteSettingsListItemViewModel> Items { get; }
    public string AddItemText => $"Add {_itemLabel}";
    public ReactiveCommand<Unit, Unit> AddItemCommand { get; }

    public ObjectListSettingViewModel(SiteSettingField field, JsonNode? value) : base(field)
    {
        _itemFields = field.ItemFields ?? [];
        _itemLabel = field.ItemLabel ?? "Entry";
        var objects = value is JsonArray array ? array.OfType<JsonObject>() : [];
        Items = new ObservableCollection<SiteSettingsListItemViewModel>(objects.Select(CreateItem));
        AddItemCommand = ReactiveCommand.Create(() => Items.Add(CreateItem(null)));
    }

    private SiteSettingsListItemViewModel CreateItem(JsonObject? value) =>
        new(new SiteSettingsFormViewModel(_itemFields, value), item => Items.Remove(item));

    public override bool TryBuild(out JsonNode? node, out string? error)
    {
        var array = new JsonArray();
        node = array;
        foreach (var (i, item) in Items.Select((item, i) => (i, item)))
        {
            if (!item.Form.TryBuild(out var itemJson, out error))
            {
                error = $"{_itemLabel} {i + 1}: {error}";
                return false;
            }

            array.Add(itemJson);
        }

        error = null;
        return true;
    }
}

public class SiteSettingsListItemViewModel : ViewModelBase
{
    public SiteSettingsFormViewModel Form { get; }
    public ReactiveCommand<Unit, Unit> RemoveCommand { get; }

    public SiteSettingsListItemViewModel(SiteSettingsFormViewModel form, Action<SiteSettingsListItemViewModel> remove)
    {
        Form = form;
        RemoveCommand = ReactiveCommand.Create(() => remove(this));
    }
}
