using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using NicheImageRipper.Core.Utility;
using ReactiveUI;

namespace NicheImageRipper.Gui.ViewModels;

/// <summary>
///     Lists the saved unfinished rips (plus any file the user browses to) with a preview of the URLs in the
///     selected one. Only valid URL lists can be loaded.
/// </summary>
public class UnfinishedUrlsViewModel : ViewModelBase
{
    public ObservableCollection<UnfinishedUrlFileViewModel> Files { get; }

    public string DirectoryPath { get; }
    public bool IsThinClient { get; }
    public bool HasFiles => Files.Count > 0;

    public UnfinishedUrlFileViewModel? SelectedFile
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(HasSelectedFile));
            this.RaisePropertyChanged(nameof(CanLoad));
            this.RaisePropertyChanged(nameof(SelectedFileIsInvalid));
        }
    }

    public bool SelectedFileIsInvalid => SelectedFile is { IsValid: false };

    public bool HasSelectedFile => SelectedFile is not null;
    public bool CanLoad => SelectedFile?.IsValid == true;

    public UnfinishedUrlsViewModel() : this("", [], false)
    {
    }

    public UnfinishedUrlsViewModel(string directoryPath, IEnumerable<UnfinishedUrlFileViewModel> files,
                                   bool isThinClient)
    {
        DirectoryPath = directoryPath;
        IsThinClient = isThinClient;
        Files = new ObservableCollection<UnfinishedUrlFileViewModel>(files);
        Files.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(HasFiles));
        SelectedFile = Files.FirstOrDefault(f => f.IsValid) ?? Files.FirstOrDefault();
    }

    /// <summary>Reads every snapshot in <paramref name="directoryPath"/>, newest first. Safe to call off the UI thread.</summary>
    public static List<UnfinishedUrlFileViewModel> ScanDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(directoryPath, "*.json")
                        .Select(path => UnfinishedUrlFileViewModel.Read(path, isExternal: false))
                        .OrderByDescending(f => f.LastModified)
                        .ToList();
    }

    /// <summary>Adds (or re-selects) a file the user browsed to, valid or not, so they can see why it can't load.</summary>
    public void AddExternalFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var existing = Files.FirstOrDefault(f => string.Equals(f.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedFile = existing;
            return;
        }

        var file = UnfinishedUrlFileViewModel.Read(fullPath, isExternal: true);
        Files.Insert(0, file);
        SelectedFile = file;
    }
}

public class UnfinishedUrlFileViewModel : ViewModelBase
{
    public string FullPath { get; }
    public string FileName => Path.GetFileName(FullPath);
    public DateTime LastModified { get; }
    public bool IsExternal { get; }
    public IReadOnlyList<string> Urls { get; }
    public bool IsValid { get; }
    public string Error { get; }

    /// <summary>Saved snapshots have random GUID names, so they're labelled by when they were last saved.</summary>
    public string Title => IsExternal ? FileName : LastModified.ToString("yyyy-MM-dd  HH:mm");

    public string Summary
    {
        get
        {
            if (!IsValid)
            {
                return Error;
            }

            var hosts = Urls.Select(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) ? uri.Host : null)
                            .Where(h => h is not null)
                            .Distinct()
                            .Take(2)
                            .ToList();
            var urlCount = Urls.Count == 1 ? "1 URL" : $"{Urls.Count} URLs";
            return hosts.Count == 0 ? urlCount : $"{urlCount} · {string.Join(", ", hosts)}";
        }
    }

    public string UrlCountText => Urls.Count == 1 ? "1 URL" : $"{Urls.Count} URLs";

    private UnfinishedUrlFileViewModel(string fullPath, DateTime lastModified, bool isExternal,
                                       IReadOnlyList<string> urls, bool isValid, string error)
    {
        FullPath = fullPath;
        LastModified = lastModified;
        IsExternal = isExternal;
        Urls = urls;
        IsValid = isValid;
        Error = error;
    }

    public static UnfinishedUrlFileViewModel Read(string path, bool isExternal)
    {
        var fullPath = Path.GetFullPath(path);
        var lastModified = File.Exists(fullPath) ? File.GetLastWriteTime(fullPath) : DateTime.MinValue;
        var isValid = UrlListFile.TryRead(fullPath, out var urls, out var error);
        return new UnfinishedUrlFileViewModel(fullPath, lastModified, isExternal, urls, isValid, error);
    }
}
