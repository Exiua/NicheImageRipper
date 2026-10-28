using NicheImageRipper.Core.GalleryUpdates;

namespace NicheImageRipper.Core.Tests;

public sealed class GalleryFileRenamerTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"nir-rename-tests-{Guid.NewGuid():N}");

    public GalleryFileRenamerTest()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_directory, name), content);

    private string Read(string name) => File.ReadAllText(Path.Combine(_directory, name));

    [Fact]
    public void ShiftsOverlappingNames()
    {
        Write("0.jpg", "a");
        Write("1.jpg", "b");

        GalleryFileRenamer.Apply(_directory, [new("0.jpg", "1.jpg"), new("1.jpg", "2.jpg")]);

        Assert.False(File.Exists(Path.Combine(_directory, "0.jpg")));
        Assert.Equal("a", Read("1.jpg"));
        Assert.Equal("b", Read("2.jpg"));
    }

    [Fact]
    public void SwapsNames()
    {
        Write("0.jpg", "a");
        Write("1.jpg", "b");

        GalleryFileRenamer.Apply(_directory, [new("0.jpg", "1.jpg"), new("1.jpg", "0.jpg")]);

        Assert.Equal("b", Read("0.jpg"));
        Assert.Equal("a", Read("1.jpg"));
    }

    [Fact]
    public void SkipsMissingFilesAndOverwritesUntrackedTargets()
    {
        Write("0.jpg", "a");
        Write("5.jpg", "leftover");

        GalleryFileRenamer.Apply(_directory, [new("missing.jpg", "4.jpg"), new("0.jpg", "5.jpg")]);

        Assert.False(File.Exists(Path.Combine(_directory, "4.jpg")));
        Assert.Equal("a", Read("5.jpg"));
        Assert.Single(Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public void MovesDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "0"));
        Write(Path.Combine("0", "inner.txt"), "x");

        GalleryFileRenamer.Apply(_directory, [new("0", "1")]);

        Assert.Equal("x", Read(Path.Combine("1", "inner.txt")));
    }
}
