using System.Reflection;
using Core.DataStructures;
using Core.Enums;
using Core.FileDownloading;

namespace CoreTest;

public class GDriveTest
{
    private readonly ITestOutputHelper _output;

    public GDriveTest(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public async Task DownloadGDriveFileTest()
    {
        Directory.SetCurrentDirectory("../../../../Workspace");
        
        var type = typeof(ImageRipper);
        var method = type.GetMethod(
            "DownloadGDriveFile",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        Assert.NotNull(method);
        var imageLink = new ImageLink
        {
            Referer = "",
            LinkInfo = LinkInfo.GDrive,
            Url = "",
            Filename = "11",
        };
        
        const string path = "TestFiles/GDriveTestDownloadFile";
        Directory.CreateDirectory(path);
        var parameters = new object?[] { path, imageLink };
        
        var task = (Task<bool>)method.Invoke(null, parameters)!;
        var result = await task;
        
        Assert.True(result);
    }

    [Fact]
    public async Task DownloadGDriveFolderTest()
    {
        const string folderUrl = "";
        const string path = "TestFiles/GDriveTestDownloadFolder";
        
        Directory.SetCurrentDirectory("../../../../Workspace");
        
        var ripInfo = new RipInfo
        {
            Urls = [],
            DirectoryName = "GDriveTest",
        };

        var type = ripInfo.GetType();
        var method = type.GetMethod(
            "QueryGDriveLinks",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        
        Assert.NotNull(method);
        
        object[] parameters =
        [
            folderUrl,
            0
        ];
        
        var task = (Task<(List<ImageLink>, int)>)method.Invoke(ripInfo, parameters)!;
        var (imageLinks, _) = await task;
        
        var ripType = typeof(ImageRipper);
        var ripMethod = ripType.GetMethod(
            "DownloadGDriveFile",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        
        Assert.NotNull(ripMethod);
        
        Directory.CreateDirectory(path);
        foreach (var link in imageLinks)
        {
            parameters = [path, link];
            var downloadTask = (Task<bool>)ripMethod.Invoke(null, parameters)!;
            var result = await downloadTask;
            Assert.True(result);
        }
        
        Assert.NotNull(ripMethod);
    }

    [Fact]
    public async Task QueryGDriveLinksTest()
    {
        Directory.SetCurrentDirectory("../../../../Workspace");

        var ripInfo = new RipInfo
        {
            Urls = [],
            DirectoryName = "GDriveTest",
        };

        var type = ripInfo.GetType();
        var method = type.GetMethod(
            "QueryGDriveLinks",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        
        Assert.NotNull(method);
        
        object[] parameters =
        [
            "",
            0
        ];
        
        var task = (Task<(List<ImageLink>, int)>)method.Invoke(ripInfo, parameters)!;
        var (imageLinks, _) = await task;
        _output.WriteLine($"Found {imageLinks.Count} links:");
        _output.WriteLine(imageLinks.Select(l => l.Url).Aggregate((a, b) => a + "\n" + b));
    }
}