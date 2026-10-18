using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Core.Tests;

/// <summary>
///     Points <see cref="AppData"/> at a temp folder for the tests that read or write app data (history database,
///     unfinished rips), so they never touch a real data folder. Runs them alone, since the folder is process-wide.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DataFolderCollection : ICollectionFixture<DataFolderFixture>
{
    public const string Name = "DataFolder";
}

public sealed class DataFolderFixture : IDisposable
{
    private readonly string _originalDirectory = AppData.Directory;

    public string Directory { get; } = Path.Combine(Path.GetTempPath(), $"nir-core-tests-{Guid.NewGuid():N}");

    public DataFolderFixture()
    {
        AppData.Directory = Directory;
    }

    public void Dispose()
    {
        // The history singleton keeps its database open for the rest of the run, so the folder may be left behind
        AppData.Directory = _originalDirectory;
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
