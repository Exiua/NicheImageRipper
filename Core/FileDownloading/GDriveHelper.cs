using System.Text;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using File = Google.Apis.Drive.v3.Data.File;

namespace NicheImageRipper.Core.FileDownloading;

public static class GDriveHelper
{
    public static async Task<DriveService> AuthenticateGDrive(CancellationToken cancellationToken = default)
    {
        var creds = await TokenManager.GDriveAuthenticate();
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = creds,
            ApplicationName = "GDriveHelper"
        });
    }
    
    public static async Task<(List<ImageLink>, int)> QueryGDriveLinks(string gDriveUrl, int index, FilenameScheme filenameScheme)
    {
        var service = await AuthenticateGDrive();
        var (id, singleFile) = ExtractId(gDriveUrl);
        var files = await service.GetFiles(id);
        var imageLinks = new List<ImageLink>();
        var counter = index;
        foreach (var file in files)
        {
            if (file.IsFolder)
            {
                continue;
            }
            
            var imgLink = new ImageLink(file.Id, filenameScheme, counter, filename: file.GetPath(),
                linkInfo: LinkInfo.GDrive);
            imageLinks.Add(imgLink);
            counter++;
        }

        return (imageLinks, counter);
    }
    
    private static (string, bool) ExtractId(string url)
    {
        var parts = url.Split("/");
        if (url.Contains("/d/"))
        {
            return (parts[^2], true);
        }

        var id = parts[^1].Split('?')[0];
        if (id is "open" or "folderview")
        {
            id = parts[^1].Split("?id=")[^1];
        }

        return (id, false);
    }
}

public class GDriveItem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsFolder { get; set; }
    private GDriveItem? Parent { get; set; }

    public GDriveItem(File file)
    {
        Id = file.Id;
        Name = file.Name;
        IsFolder = file.MimeType == "application/vnd.google-apps.folder";
    }

    public GDriveItem(File file, GDriveItem parent)
    {
        Id = file.Id;
        Name = file.Name;
        IsFolder = file.MimeType == "application/vnd.google-apps.folder";
        Parent = parent;
    }

    public string GetPath()
    {
        if (Parent is null)
        {
            return "";
        }
        
        var path = new StringBuilder();
        Parent.GetPath(path);
        path.Append(Name);
        if (IsFolder)
        {
            path.Append('/');
        }
        
        return path.ToString();
    }

    private void GetPath(StringBuilder stringBuilder)
    {
        Parent?.GetPath(stringBuilder);
        stringBuilder.Append(Name);
        stringBuilder.Append('/');
    }
    
    public override string ToString()
    {
        return $"{{{Id}: {Name}}}";
    }
}

internal static class GDriveExtensionMethods
{
    public static async Task<List<File>> GetChildren(this DriveService service, string folderId)
    {
        var request = service.Files.List();
        request.Q = $"'{folderId}' in parents";
        request.PageSize = 100;
        request.Fields = "files(modifiedTime,id,parents,name,webContentLink,mimeType)";
        var files = new List<File>();
        do
        {
            var children = await request.ExecuteAsync();
            files.AddRange(children.Files);
            request.PageToken = children.NextPageToken;
            await Task.Delay(10);
        } while (!string.IsNullOrEmpty(request.PageToken));
        
        return files;
    }
    
    public static async Task<List<GDriveItem>> GetFiles(this DriveService service, string id, GDriveItem? parent = null)
    {
        var files = new List<GDriveItem>();
        if (parent is null)
        {
            var file = await service.Files.Get(id).ExecuteAsync();
            parent = new GDriveItem(file);
            files.Add(parent);
        }
        
        var children = await service.GetChildren(id);
        foreach (var gDriveItem in children.Select(child => new GDriveItem(child, parent)))
        {
            files.Add(gDriveItem);
            if (gDriveItem.IsFolder)
            {
                var childFiles = await service.GetFiles(gDriveItem.Id, gDriveItem);
                files.AddRange(childFiles);
            }
        }

        return files;
    }
}