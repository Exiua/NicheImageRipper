using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;
using ReactiveUI;

namespace NicheImageRipper.Gui.ViewModels;

/// <summary>
///     Editable copy of every per-site config. Nothing is applied until the caller reads
///     <see cref="BuildSiteConfigs"/>, so cancelling the dialog leaves the real config untouched.
/// </summary>
public class SiteConfigEditorViewModel : ViewModelBase
{
    private readonly HashSet<string> _knownSiteNames;

    public ObservableCollection<SiteConfigEntryViewModel> Sites { get; }
    public IReadOnlyList<string> KnownSiteNames { get; }
    public FilenameScheme[] FilenameSchemes { get; } = Enum.GetValues<FilenameScheme>();

    public SiteConfigEntryViewModel? SelectedSite
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(HasSelectedSite));
        }
    }

    public bool HasSelectedSite => SelectedSite is not null;

    public string NewSiteName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string ErrorMessage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public ReactiveCommand<Unit, Unit> AddSiteCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveSiteCommand { get; }

    public SiteConfigEditorViewModel()
        : this(new Dictionary<string, SiteConfig>(), [])
    {
    }

    public SiteConfigEditorViewModel(IReadOnlyDictionary<string, SiteConfig> sites, IReadOnlyList<string> knownSiteNames)
    {
        _knownSiteNames = new HashSet<string>(knownSiteNames, StringComparer.OrdinalIgnoreCase);
        KnownSiteNames = knownSiteNames;
        Sites = new ObservableCollection<SiteConfigEntryViewModel>(
            sites.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                 .Select(kv => new SiteConfigEntryViewModel(kv.Key, kv.Value, IsKnown(kv.Key))));
        SelectedSite = Sites.FirstOrDefault();

        var canAdd = this.WhenAnyValue(x => x.NewSiteName, name => !string.IsNullOrWhiteSpace(name));
        AddSiteCommand = ReactiveCommand.Create(AddSite, canAdd);
        var canRemove = this.WhenAnyValue(x => x.SelectedSite).Select(site => site is not null);
        RemoveSiteCommand = ReactiveCommand.Create(RemoveSelectedSite, canRemove);
    }

    private bool IsKnown(string name) => _knownSiteNames.Count == 0 || _knownSiteNames.Contains(name);

    private void AddSite()
    {
        var name = NewSiteName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        var existing = Sites.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedSite = existing;
            NewSiteName = "";
            return;
        }

        // Use the canonical casing of a known parser name if the user typed it differently
        name = KnownSiteNames.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) ?? name;
        var entry = new SiteConfigEntryViewModel(name, new SiteConfig(), IsKnown(name));
        var index = 0;
        while (index < Sites.Count && string.Compare(Sites[index].Name, name, StringComparison.OrdinalIgnoreCase) < 0)
        {
            index++;
        }

        Sites.Insert(index, entry);
        SelectedSite = entry;
        NewSiteName = "";
    }

    private void RemoveSelectedSite()
    {
        if (SelectedSite is null)
        {
            return;
        }

        var index = Sites.IndexOf(SelectedSite);
        Sites.RemoveAt(index);
        SelectedSite = Sites.Count == 0 ? null : Sites[Math.Min(index, Sites.Count - 1)];
    }

    /// <summary>Checks every entry; on failure selects the offending site and sets <see cref="ErrorMessage"/>.</summary>
    public bool Validate()
    {
        foreach (var site in Sites)
        {
            if (!site.TryBuild(out _, out var error))
            {
                SelectedSite = site;
                ErrorMessage = $"{site.Name}: {error}";
                return false;
            }
        }

        ErrorMessage = "";
        return true;
    }

    public Dictionary<string, SiteConfig> BuildSiteConfigs()
    {
        var result = new Dictionary<string, SiteConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var site in Sites)
        {
            if (!site.TryBuild(out var config, out var error))
            {
                throw new InvalidOperationException($"Invalid site config for {site.Name}: {error}");
            }

            result[site.Name] = config;
        }

        return result;
    }
}

public class SiteConfigEntryViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    public string Name { get; }

    /// <summary>False when the name doesn't match any loaded parser, so the config would never be read.</summary>
    public bool IsKnownSite { get; }

    public bool HasLogin
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Username
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Password
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool RevealPassword
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Key
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>One cookie value per line.</summary>
    public string CookiesText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool HasOverrides
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public FilenameScheme OverrideFilenameScheme
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string PostProcessScript
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string CustomJson
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public SiteConfigEntryViewModel(string name, SiteConfig config, bool isKnownSite)
    {
        Name = name;
        IsKnownSite = isKnownSite;
        HasLogin = config.Login is not null;
        Username = config.Login?.Username ?? "";
        Password = config.Login?.Password ?? "";
        Key = config.Key ?? "";
        CookiesText = string.Join(Environment.NewLine, config.Cookies ?? []);
        HasOverrides = config.Overrides is not null;
        OverrideFilenameScheme = config.Overrides?.FilenameScheme ?? FilenameScheme.Original;
        PostProcessScript = config.PostProcessScript ?? "";
        CustomJson = config.Custom is { } custom ? JsonSerializer.Serialize(custom, IndentedJson) : "";
    }

    public bool TryBuild(out SiteConfig config, out string? error)
    {
        config = new SiteConfig();
        error = null;

        if (!string.IsNullOrWhiteSpace(CustomJson))
        {
            try
            {
                using var document = JsonDocument.Parse(CustomJson);
                config.Custom = document.RootElement.Clone();
            }
            catch (JsonException e)
            {
                error = $"Custom JSON is invalid: {e.Message}";
                return false;
            }
        }

        if (HasLogin)
        {
            config.Login = new Credentials { Username = Username, Password = Password };
        }

        if (HasOverrides)
        {
            config.Overrides = new SettingsOverride { FilenameScheme = OverrideFilenameScheme };
        }

        config.Key = NullIfBlank(Key);
        config.PostProcessScript = NullIfBlank(PostProcessScript);

        var cookies = CookiesText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        config.Cookies = cookies.Length == 0 ? null : cookies;

        return true;
    }

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
