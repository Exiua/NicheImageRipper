using System.Text;
using Google;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using Serilog;
using File = Google.Apis.Drive.v3.Data.File;

namespace NicheImageRipper.SiteModules.Modules.Google;

public static class GDriveHelper
{
    private static readonly ILogger Log = Serilog.Log.ForContext(typeof(GDriveHelper));
    private static readonly SemaphoreSlim ServiceLock = new(1, 1);
    private static DriveService? _service;

    /// <summary>
    ///     The Drive client, authenticated on first use and reused afterward: authorizing reads the credentials and
    ///     token cache from disk (and may prompt), which is far too slow to repeat for every file.
    /// </summary>
    public static async Task<DriveService> AuthenticateGDrive(CancellationToken cancellationToken = default)
    {
        if (_service is not null)
        {
            return _service;
        }

        await ServiceLock.WaitAsync(cancellationToken);
        try
        {
            return _service ??= new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = await GoogleAuth.GDriveAuthenticate(),
                ApplicationName = "NicheImageRipper"
            });
        }
        finally
        {
            ServiceLock.Release();
        }
    }
    
    /// <summary>
    ///     Drops the cached Drive client so the next <see cref="AuthenticateGDrive"/> call authorizes from scratch.
    /// </summary>
    private static async Task ResetService(DriveService staleService)
    {
        await ServiceLock.WaitAsync();
        try
        {
            // Another caller may have already replaced it
            if (ReferenceEquals(_service, staleService))
            {
                _service = null;
                staleService.Dispose();
            }
        }
        finally
        {
            ServiceLock.Release();
        }
    }

    /// <summary>
    ///    Expands a Google Drive link into one <see cref="FileLink"/> per file, keeping the folder structure inside the gallery.
    /// </summary>
    /// <param name="gDriveUrl">The Google Drive URL to expand. Can be a single-file link or a folder link.</param>
    /// <param name="index">The starting index for numbering the files in the gallery. This is used to generate unique filenames.</param>
    /// <param name="filenameScheme">The scheme to use for generating filenames for the files in the gallery.</param>
    /// <returns>Tuple containing a list of <see cref="FileLink"/> objects representing the files in the Google Drive link, and the next index to use for numbering files.</returns>
    public static async Task<(List<FileLink>, int)> QueryGDriveLinks(string gDriveUrl, int index, FilenameScheme filenameScheme)
    {
        var service = await AuthenticateGDrive();
        var (id, singleFile) = ExtractId(gDriveUrl);
        List<GDriveItem> files;
        try
        {
            files = await service.GetFiles(id);
        }
        catch (TokenResponseException e) when (e.Error?.Error == "invalid_grant")
        {
            // The refresh token was expired or revoked. The auth library has already deleted it from the token
            // store, so re-authorizing prompts the user to sign in again instead of reusing the dead credential.
            await ResetService(service);
            service = await AuthenticateGDrive();
            files = await service.GetFiles(id);
        }
        catch (GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Log.Warning("GDrive link {GDriveUrl} is no longer available: {EMessage}", gDriveUrl, e.Message);
            return (new List<FileLink>(), index);
        }

        var fileLinks = new List<FileLink>();
        var counter = index;
        foreach (var file in files.Where(f => !f.IsFolder))
        {
            // Drive names are user-controlled: clean them, keeping the folder structure inside the gallery
            var fileLink = FileLink.WithFilename(file.Id, file.GetPath(), filenameScheme, index: counter,
                linkInfo: GDriveLinkInfo.GDrive, filenameCleaning: FilenameCleaning.PreserveSubdirectories);
            fileLinks.Add(fileLink);
            counter++;
        }

        return (fileLinks, counter);
    }
    
    internal static (string Id, bool SingleFile) ExtractId(string url)
    {
        // Links scraped from post bodies can carry trailing newlines/whitespace
        url = url.Trim();
        var parts = url.Split("/");
        if (url.Contains("/d/"))
        {
            return url.Contains("/file/") ? (parts[5], true) : (parts[^2], true);
        }

        var id = parts[^1].Split('?')[0];
        if (id is "open" or "folderview")
        {
            // e.g. open?id=<id>&usp=sharing
            id = parts[^1].Split("?id=")[^1].Split('&')[0];
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
            // A root file is a single-file link: it still needs its own name, or FileLink falls back to parsing
            // the bare Drive ID as a URL
            return IsFolder ? "" : Name;
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