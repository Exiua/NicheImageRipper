namespace Core.ArgParse;

public class Arguments
{
    public bool Debug { get; set; }
    #if DEBUG
    public RunMode RunMode { get; set; } = RunMode.Test;
    #else
    public RunMode RunMode { get; set; } = RunMode.Cli;
    #endif
    
    public string? Url { get; set; }
    public bool PrintSite { get; set; }
    public int NumThreads { get; set; }
}