using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Sdk.Tests;

public class FilesystemUtilityTest
{
    [Theory]
    [InlineData("file.jpg", "file.jpg")]
    [InlineData("sub/dir/file.jpg", "subdirfile.jpg")]
    [InlineData("  fi?le:name*.jpg  ", "filename.jpg")]
    [InlineData("...", "")]
    public void CleanPathStemRemovesSeparators(string input, string expected)
    {
        Assert.Equal(expected, FilesystemUtility.CleanPathStem(input));
    }

    [Theory]
    [InlineData("file.jpg", "file.jpg")]
    [InlineData("sub/dir/file.jpg", "sub/dir/file.jpg")]
    [InlineData(@"sub\dir\file.jpg", "sub/dir/file.jpg")]
    [InlineData("sub:dir/fi?le.jpg", "subdir/file.jpg")]
    [InlineData("sub//dir/", "sub/dir")]
    [InlineData("/absolute/file.jpg", "absolute/file.jpg")]
    [InlineData("./file.jpg", "file.jpg")]
    [InlineData("../../evil.jpg", "evil.jpg")]
    [InlineData(@"sub\..\..\evil.jpg", "sub/evil.jpg")]
    [InlineData("C:/Windows/evil.jpg", "C/Windows/evil.jpg")]
    [InlineData("../..", "")]
    public void CleanPathStemPreservingSeparatorsStaysRelative(string input, string expected)
    {
        Assert.Equal(expected, FilesystemUtility.CleanPathStem(input, preserveSeparators: true));
    }

    [Theory]
    [InlineData(FilenameCleaning.None, "../sub/fi?le.jpg")]
    [InlineData(FilenameCleaning.PreserveSubdirectories, "sub/file.jpg")]
    [InlineData(FilenameCleaning.Full, "subfile.jpg")]
    public void FileLinkAppliesFilenameCleaning(FilenameCleaning cleaning, string expected)
    {
        var link = FileLink.WithFilename("https://example.com/x", "../sub/fi?le.jpg", FilenameScheme.Original,
            filenameCleaning: cleaning);

        Assert.Equal(expected, link.Filename);
    }
}
