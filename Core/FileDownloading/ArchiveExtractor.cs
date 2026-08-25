using System.IO.Compression;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Utility;
using Sdk.Configuration;
using Serilog;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace NicheImageRipper.Core.FileDownloading;

public sealed class ArchiveExtractor(UnzipProtocol unzipProtocol, ILogger logger)
{
    public (int Extracted, int Failed) ExtractAll(string directoryPath)
    {
        var count = 0;
        var error = 0;

        var files = Directory.GetFiles(directoryPath, "*.zip", SearchOption.AllDirectories);
        var (c1, e1) = ExtractAndCount(files, UnzipZipFile);
        count += c1; error += e1;

        files = Directory.GetFiles(directoryPath, "*.7z", SearchOption.AllDirectories);
        var (c2, e2) = ExtractAndCount(files, file =>
        {
            using var archive = SevenZipArchive.OpenArchive(file);
            UncompressFile(file, archive);
        });
        count += c2; error += e2;

        files = Directory.GetFiles(directoryPath, "*.rar", SearchOption.AllDirectories);
        var (c3, e3) = ExtractAndCount(files, file =>
        {
            using var archive = RarArchive.OpenArchive(file);
            UncompressFile(file, archive);
        });
        count += c3; error += e3;

        return (count, error);
    }

    private (int Count, int Error) ExtractAndCount(string[] files, Action<string> extractAction)
    {
        var count = 0;
        var error = 0;
        foreach (var file in files)
        {
            try
            {
                extractAction(file);
                count++;
            }
            catch (Exception)
            {
                logger.Error("Failed to extract: {File}", file);
                error++;
            }
        }

        return (count, error);
    }

    private static void UncompressFile(string archivePath, IArchive archive)
    {
        var extractPath = Path.ChangeExtension(archivePath, null);
        Directory.CreateDirectory(extractPath);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            entry.WriteToDirectory(extractPath, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }
    }

    private void UnzipZipFile(string zipPath)
    {
        var extractPath = Path.ChangeExtension(zipPath, null);
        Directory.CreateDirectory(extractPath);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, extractPath);
        }
        catch (InvalidDataException)
        {
            var ext = FileUtility.GetCorrectExtension(zipPath);
            var newPath = Path.ChangeExtension(zipPath, ext);
            File.Move(zipPath, newPath);
            return;
        }

        if (unzipProtocol == UnzipProtocol.ExtractDelete)
        {
            File.Delete(zipPath);
        }
    }
}