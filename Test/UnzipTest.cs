using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace Test;

public class UnzipTest
{
    public static void UnRarTest(string filepath)
    {
        const string dest = "./Test/UnzipTest/UnRarTest";
        Directory.CreateDirectory(dest);
        using var archive = RarArchive.Open(filepath);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            entry.WriteToDirectory(dest, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }
    }
    
    public static void Un7ZipTest(string filepath)
    {
        const string dest = "./Test/UnzipTest/Un7ZipTest";
        Directory.CreateDirectory(dest);
        using var archive = SevenZipArchive.Open(filepath);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            entry.WriteToDirectory(dest, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }
    }
}