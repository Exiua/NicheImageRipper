using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Sdk.Tests;

/// <summary>Points <see cref="AppData"/> at a fresh temp folder per test and restores it afterwards.</summary>
[Collection(AppDataCollection.Name)]
public sealed class AppDataTest : IDisposable
{
    private readonly string _originalDirectory = AppData.Directory;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"nir-appdata-{Guid.NewGuid():N}");

    public AppDataTest()
    {
        AppData.Directory = _directory;
    }

    public void Dispose()
    {
        AppData.Directory = _originalDirectory;
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void DirectoryIsStoredAsAFullPath()
    {
        AppData.Directory = Path.Combine(_directory, "nested", "..", "data");

        Assert.Equal(Path.Combine(_directory, "data"), AppData.Directory);
    }

    [Fact]
    public void PathsResolveInsideTheDataFolder()
    {
        Assert.Equal(Path.Combine(_directory, "config.json"), AppData.GetPath("config.json"));
        Assert.Equal(Path.Combine(_directory, "Logs", "gui.log"), AppData.GetPath("Logs", "gui.log"));
        Assert.Equal(Path.Combine(_directory, "Logs", "gui.log"), AppData.GetPath(Path.Combine("Logs", "gui.log")));
    }

    [Fact]
    public void PathsDoNotDependOnTheWorkingDirectory()
    {
        var originalWorkingDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = Path.GetTempPath();

            Assert.Equal(Path.Combine(_directory, "config.json"), AppData.GetPath("config.json"));
        }
        finally
        {
            Environment.CurrentDirectory = originalWorkingDirectory;
        }
    }

    [Fact]
    public void EnsureDirectoryCreatesTheFolder()
    {
        var path = AppData.EnsureDirectory("UnfinishedRips", "nested");

        Assert.True(Directory.Exists(path));
        Assert.Equal(Path.Combine(_directory, "UnfinishedRips", "nested"), path);
    }

    [Fact]
    public void WritesCreateMissingFolders()
    {
        var file = Path.Combine("Logs", "deep", "failed.txt");

        AppData.WriteAllText(file, "first");

        Assert.True(AppData.Exists(file));
        Assert.Equal("first", AppData.ReadAllText(file));
    }

    [Fact]
    public void PathForWriteCreatesTheFolderButNotTheFile()
    {
        // Used for the database connection string: SQLite creates the file, but not a missing data folder
        var path = AppData.GetPathForWrite("NicheImageRipper.db");

        Assert.Equal(Path.Combine(_directory, "NicheImageRipper.db"), path);
        Assert.True(Directory.Exists(_directory));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void AppendAddsToTheFile()
    {
        AppData.AppendAllText("passwords.txt", "a\n");
        AppData.AppendAllText("passwords.txt", "b\n");

        Assert.Equal("a\nb\n", AppData.ReadAllText("passwords.txt"));
    }

    [Fact]
    public async Task AsyncReadAndWriteRoundTrip()
    {
        var ct = TestContext.Current.CancellationToken;
        await AppData.WriteAllTextAsync(Path.Combine("sub", "state.json"), "{}", ct);
        await AppData.AppendAllTextAsync(Path.Combine("sub", "state.json"), "\n", ct);

        Assert.Equal("{}\n", await AppData.ReadAllTextAsync(Path.Combine("sub", "state.json"), ct));
    }

    [Fact]
    public void OpenWriteReplacesTheFile()
    {
        AppData.WriteAllText("cookies.txt", "a much longer original value");
        using (var stream = AppData.OpenWrite("cookies.txt"))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write("short");
        }

        using var reader = new StreamReader(AppData.OpenRead("cookies.txt"));
        Assert.Equal("short", reader.ReadToEnd());
    }

    [Fact]
    public void MissingFileDoesNotExist()
    {
        Assert.False(AppData.Exists("client_secrets.json"));
    }
}

/// <summary>Tests that change the process-wide <see cref="AppData.Directory"/> run alone, not alongside others.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppDataCollection
{
    public const string Name = "AppData";
}
