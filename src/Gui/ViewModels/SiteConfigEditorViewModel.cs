using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
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
    private readonly IReadOnlyDictionary<string, GalleryUpdateSupport> _updateSupport;

    public ObservableCollection<SiteConfigEntryViewModel> Sites { get; }
    public IReadOnlyList<string> KnownSiteNames { get; }
    public FilenameScheme[] FilenameSchemes { get; } = Enum.GetValues<FilenameScheme>();
    public IReadOnlyList<GalleryUpdateModeOption> GalleryUpdateModeOptions => GalleryUpdateModeOption.All;

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
        : this(new Dictionary<string, SiteConfig>(), [], new Dictionary<string, GalleryUpdateSupport>())
    {
    }

    /// <param name="updateSupport">The update strategies of each updatable site, keyed by parser name.</param>
    public SiteConfigEditorViewModel(IReadOnlyDictionary<string, SiteConfig> sites, IReadOnlyList<string> knownSiteNames,
                                     IReadOnlyDictionary<string, GalleryUpdateSupport> updateSupport)
    {
        _knownSiteNames = new HashSet<string>(knownSiteNames, StringComparer.OrdinalIgnoreCase);
        _updateSupport = new Dictionary<string, GalleryUpdateSupport>(updateSupport, StringComparer.OrdinalIgnoreCase);
        KnownSiteNames = knownSiteNames;
        Sites = new ObservableCollection<SiteConfigEntryViewModel>(
            sites.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                 .Select(kv => CreateEntry(kv.Key, kv.Value)));
        SelectedSite = Sites.FirstOrDefault();

        var canAdd = this.WhenAnyValue(x => x.NewSiteName, name => !string.IsNullOrWhiteSpace(name));
        AddSiteCommand = ReactiveCommand.Create(AddSite, canAdd);
        var canRemove = this.WhenAnyValue(x => x.SelectedSite).Select(site => site is not null);
        RemoveSiteCommand = ReactiveCommand.Create(RemoveSelectedSite, canRemove);
    }

    private bool IsKnown(string name) => _knownSiteNames.Count == 0 || _knownSiteNames.Contains(name);

    private SiteConfigEntryViewModel CreateEntry(string name, SiteConfig config) =>
        new(name, config, IsKnown(name), _updateSupport.GetValueOrDefault(name));

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
        var entry = CreateEntry(name, new SiteConfig());
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

    public GalleryUpdateModeOption OverrideGalleryUpdateMode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>The parser's default followed by every strategy it supports.</summary>
    public IReadOnlyList<GalleryUpdateStrategyOption> GalleryUpdateStrategyOptions { get; }

    /// <summary>Only parsers supporting more than one strategy have anything to choose, unless an override is set
    /// that needs clearing.</summary>
    public bool CanChooseUpdateStrategy { get; }

    public GalleryUpdateStrategyOption OverrideGalleryUpdateStrategy
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <param name="updateSupport">Null when the site isn't updatable (or unknown).</param>
    public SiteConfigEntryViewModel(string name, SiteConfig config, bool isKnownSite,
                                    GalleryUpdateSupport? updateSupport = null)
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
        OverrideGalleryUpdateMode = GalleryUpdateModeOption.For(config.Overrides?.GalleryUpdateMode);
        CanChooseUpdateStrategy = updateSupport is { Supported.Count: > 1 } || config.Overrides?.GalleryUpdateStrategy is not null;
        GalleryUpdateStrategyOptions = GalleryUpdateStrategyOption.For(updateSupport, config.Overrides?.GalleryUpdateStrategy);
        OverrideGalleryUpdateStrategy = GalleryUpdateStrategyOptions.First(o =>
            o.Strategy == config.Overrides?.GalleryUpdateStrategy);
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
            config.Overrides = new SettingsOverride
            {
                FilenameScheme = OverrideFilenameScheme,
                GalleryUpdateMode = OverrideGalleryUpdateMode.Mode,
                GalleryUpdateStrategy = OverrideGalleryUpdateStrategy.Strategy,
            };
        }

        config.Key = NullIfBlank(Key);
        config.PostProcessScript = NullIfBlank(PostProcessScript);

        var cookies = CookiesText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        config.Cookies = cookies.Length == 0 ? null : cookies;

        return true;
    }

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>A per-site gallery update mode choice; a null <see cref="Mode"/> inherits the global setting.</summary>
public sealed record GalleryUpdateModeOption(GalleryUpdateMode? Mode)
{
    public static IReadOnlyList<GalleryUpdateModeOption> All { get; } =
        [new((GalleryUpdateMode?)null), ..Enum.GetValues<GalleryUpdateMode>().Select(mode => new GalleryUpdateModeOption(mode))];

    public static GalleryUpdateModeOption For(GalleryUpdateMode? mode) => All.FirstOrDefault(o => o.Mode == mode) ?? All[0];

    public override string ToString() => Mode?.ToString() ?? "Use global setting";
}

/// <summary>A per-site gallery update strategy choice; a null <see cref="Strategy"/> uses the parser's default.</summary>
public sealed record GalleryUpdateStrategyOption(GalleryUpdateStrategy? Strategy, string Label)
{
    /// <summary>
    ///     The parser default followed by each supported strategy. A <paramref name="current"/> override the parser
    ///     doesn't support is listed too, so opening and saving the editor doesn't silently drop it.
    /// </summary>
    public static IReadOnlyList<GalleryUpdateStrategyOption> For(GalleryUpdateSupport? support,
                                                                 GalleryUpdateStrategy? current)
    {
        var options = new List<GalleryUpdateStrategyOption>
        {
            new(null, support is null ? "Parser default" : $"Parser default ({support.Default})"),
        };
        options.AddRange((support?.Supported ?? []).Select(s => new GalleryUpdateStrategyOption(s, s.ToString())));
        if (current is { } strategy && options.All(o => o.Strategy != strategy))
        {
            options.Add(new GalleryUpdateStrategyOption(strategy, $"{strategy} (not supported)"));
        }

        return options;
    }

    public override string ToString() => Label;
}
