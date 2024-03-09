using Core.Enums;

namespace Core;

public class RipInfo
{
    private string _directoryName;
    
    public FilenameScheme FilenameScheme { get; set; }
    public List<ImageLink> Urls { get; set; }
    public bool MustGenerateManually { get; set; }
    
    private int UrlCount { get; set; }

    public int NumUrls => UrlCount;

    public string DirectoryName
    {
        get => _directoryName;
        set => _directoryName = value;
    }
    
    private List<string>? Filenames { get; set; }

    public RipInfo(List<string> urls, string directoryName = "", FilenameScheme filenameScheme = FilenameScheme.Original,
        bool generate = false, int numUrls = 0, List<string>? filenames = null, bool discardBlobs = false)
    {
        FilenameScheme = filenameScheme;
        DirectoryName = directoryName;
        Filenames = filenames;
    }
}