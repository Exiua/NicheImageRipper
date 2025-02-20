using Core;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Utility;
using Serilog;
using Serilog.Events;

namespace CoreTui;

public class NicheImageRipperCli : NicheImageRipper
{
    private void PrintHistory()
    {
        Console.WriteLine("+----------------------------------------+");
        Console.WriteLine("| Directory Name | URL | Date | Num URLs |");
        Console.WriteLine("+----------------------------------------+");
        var history = HistoryDb.GetHistory(1, 100);
        foreach (var entry in history)
        {
            Console.WriteLine($"| {entry.DirectoryName} | {entry.Url} | {entry.Date} | {entry.NumUrls} |");
            Console.WriteLine("+----------------------------------------+");
        }
    }
    
    public async Task Run()
    {
        var supportedFeatures = GetExternalFeatureSupport();
        if (!supportedFeatures.HasFlag(ExternalFeatureSupport.Ffmpeg))
        {
            Log.Warning("ffmpeg not found. Some functionality may be limited.");
        }
        
        if (!supportedFeatures.HasFlag(ExternalFeatureSupport.YtDlp))
        {
            Log.Warning("yt-dlp not found. Some functionality may be limited.");
        }
        
        if (!supportedFeatures.HasFlag(ExternalFeatureSupport.MegaCmd))
        {
            Log.Warning("MEGAcmd not found. Some functionality may be limited.");
        }
        
        if (!supportedFeatures.HasFlag(ExternalFeatureSupport.FlareSolverr))
        {
            Log.Warning("FlareSolverr not found. Some functionality may be limited.");
        }
        
        while (true)
        {
            try
            {
                LogMessageToFile($"{Title}> ", newLine: false);
                var userInput = Console.ReadLine();
                if (string.IsNullOrEmpty(userInput))
                {
                    continue;
                }

                var cmdParts = userInput.Split(' ');
                switch (cmdParts[0])
                {
                    case "booru":
                    {
                        if (cmdParts.Length < 2)
                        {
                            LogMessageToFile("Missing argument: <tags>", LogEventLevel.Warning);
                            break;
                        }

                        var tags = cmdParts[1];
                        var url = "https://booru.com/post?tags=" + tags;
                        QueueUrls(url);
                        break;
                    }
                    case "c" or "clear":
                        if (cmdParts.Length < 2)
                        {
                            LogMessageToFile("Missing argument: cache or queue", LogEventLevel.Warning);
                            break;
                        }
                        
                        switch (cmdParts[1])
                        {
                            case "cache":
                                ClearCache();
                                LogMessageToFile("Cache cleared");
                                break;
                            case "queue":
                                UrlQueue.Clear();
                                LogMessageToFile("Queue cleared");
                                break;
                            default:
                                LogMessageToFile("Missing argument: cache or queue", LogEventLevel.Warning);
                                break;
                        }

                        break;
                    case "config":
                    {
                        if (cmdParts.Length == 1)
                        {
                            LogMessageToFile("Missing argument: <key>", LogEventLevel.Warning);
                            break;
                        }
                        
                        var key = cmdParts[1].ToLower();
                        switch (key)
                        {
                            case "savepath":
                                if (cmdParts.Length == 2)
                                {
                                    LogMessageToFile($"Save path: {SavePath}");
                                }
                                else
                                {
                                    var path = cmdParts[2];
                                    if (FileUtility.IsValidAndEnsureDirectory(path))
                                    {
                                        SavePath = path;
                                        LogMessageToFile($"Save path set to {SavePath}");
                                    }
                                    else
                                    {
                                        LogMessageToFile("Invalid path", LogEventLevel.Warning);
                                    }
                                }

                                break;
                            case "postdownloadaction":
                                if (cmdParts.Length == 2)
                                {
                                    LogMessageToFile($"Post download action: {PostDownloadAction}");
                                }
                                else
                                {
                                    if (Enum.TryParse(cmdParts[2], true, out PostDownloadAction action))
                                    {
                                        if (action == PostDownloadAction.None)
                                        {
                                            PostDownloadAction = action;
                                        }
                                        else
                                        {
                                            if (PostDownloadAction.HasFlag(action))
                                            {
                                                PostDownloadAction &= ~action;
                                            }
                                            else
                                            {
                                                PostDownloadAction |= action;
                                            }
                                        }
                                        
                                        LogMessageToFile($"Post download action set to {PostDownloadAction}");
                                    }
                                    else
                                    {
                                        var validActions = string.Join(", ", Enum.GetNames<PostDownloadAction>());
                                        LogMessageToFile($"Invalid action. Valid actions: {{{validActions}}}", LogEventLevel.Warning);
                                    }
                                }
                                
                                break;
                            default:
                                LogMessageToFile($"Invalid key: {key}. Valid Keys: {{savepath, postdownloadaction}}", LogEventLevel.Warning);
                                break;
                        }
                        break;
                    }
                    case "debug":
                        Log.Warning("Debug mode is not yet implemented.");
                        //HtmlParser.SetDebugMode(true);
                        Debugging = true;
                        break;
                    case "delay":
                        if(cmdParts.Length < 2)
                        {
                            LogMessageToFile($"Retry delay: {RetryDelay} ms");
                        }
                        else
                        {
                            if(int.TryParse(cmdParts[1], out var delay))
                            {
                                RetryDelay = delay;
                                LogMessageToFile($"Retry delay set to {delay} ms");
                            }
                            else
                            {
                                LogMessageToFile("Invalid argument", LogEventLevel.Warning);
                            }
                        }

                        break;
                    case "help":
                        LogMessageToFile("""
                                         Commands:
                                         - q(uit): Exit the REPL, saving all data and disposing of resources.
                                         - r(ip): Start the ripping process.
                                         - queue: Display all URLs currently in the queue.
                                         - c(lear) cache: Clear the cache.
                                         - c(lear) queue: Clear the URL queue.
                                         - retries [n]: Get or set the maximum number of retries (default: current value).
                                         - delay [ms]: Get or set the delay between retries in milliseconds (default: current value).
                                         - skip [index]: Skip a URL at a specific index in the queue (default: first URL).
                                         - debug: Enable debug mode for the HTML parser.
                                         - save: Save the current state and data.
                                         - history: Display the history of processed URLs or actions.
                                         - l(oad) [filename]: Load URLs from a specified file (default: 'UnfinishedRips.json').
                                         - peek | head: Display the first URL in the queue without removing it.
                                         - tail: Display the last URL in the queue without removing it.
                                         - regen: Regenerate the HTML parser driver.
                                         - [URL(s)]: Queue a URL or list of URLs for processing. Handles failures with options for re-queuing.
                                         """);
                        break;
                    case "history":
                        PrintHistory();
                        break;
                    case "l" or "load":
                        LoadUrlFile(cmdParts.Length < 2 ? "UnfinishedRips.json" : cmdParts[1]);
                        LogMessageToFile("URLs loaded");
                        break;
                    case "list":
                        var message = "";
                        foreach (var (i, queuedUrl) in UrlQueue.Enumerate())
                        {
                            message += $"{i}: {queuedUrl}\n";
                        }
                        
                        LogMessageToFile(message);
                        break;
                    case "lookup":
                    {
                        if (cmdParts.Length < 2)
                        {
                            LogMessageToFile("Missing argument: <folder_name>", LogEventLevel.Warning);
                        }

                        var remainder = cmdParts[1..].Join(" ");
                        var folderName = ExtractionUtility.ExtractFolderNameFromString(remainder);
                        if (folderName is null)
                        {
                            LogMessageToFile("Invalid folder name", LogEventLevel.Warning);
                            break;
                        }

                        var historyEntry = HistoryDb.GetHistoryEntryByDirectoryName(folderName);
                        if (historyEntry is null)
                        {
                            LogMessageToFile("No history entry found", LogEventLevel.Warning);
                            break;
                        }
                        
                        LogMessageToFile($"[{historyEntry.Date:s}] Url: {historyEntry.Url}");
                        break;
                    }
                    case "merge":
                        if (cmdParts.Length < 2)
                        {
                            LogMessageToFile("Missing argument: filename", LogEventLevel.Warning);
                            break;
                        }
                        
                        HistoryDb.MergeHistory(cmdParts[1]);
                        LogMessageToFile("History merged");
                        break;
                    case "peek" or "head":
                        LogMessageToFile(UrlQueue.Count == 0 ? "Queue is empty" : UrlQueue[0]);
                        break;
                    case "q" or "quit":
                        LogMessageToFile("Exiting...");
                        SaveData();
                        //HtmlParser.Dispose();
                        return;
                    case "queue":
                        LogMessageToFile(string.Join("\n", UrlQueue));
                        break;
                    case "r" or "rip":
                        await Rip();
                        break;
                    case "regen":
                        Log.Warning("Regenerating the HTML parser driver is not yet implemented.");
                        //HtmlParser.RegenerateDriver();
                        break;
                    case "retries":
                        if (cmdParts.Length < 2)
                        {
                            LogMessageToFile($"Max retries: {MaxRetries - 1}");
                        }
                        else
                        {
                            if(int.TryParse(cmdParts[1], out var retries))
                            {
                                MaxRetries = retries + 1;
                                LogMessageToFile($"Max retries set to {MaxRetries}");
                            }
                            else
                            {
                                LogMessageToFile("Invalid argument", LogEventLevel.Warning);
                            }
                        }

                        break;
                    case "save":
                        SaveData();
                        LogMessageToFile("Data saved");
                        break;
                    case "skip":
                    {
                        if (UrlQueue.Count == 0)
                        {
                            LogMessageToFile("Queue is empty");
                            break;
                        }

                        var index = 0;
                        if (cmdParts.Length >= 2)
                        {
                            if (int.TryParse(cmdParts[1], out var i))
                            {
                                index = i;
                            }
                            else
                            {
                                LogMessageToFile("Invalid argument");
                                break;
                            }
                        }

                        var normalizedIndex = Math.Abs(index);
                        if (normalizedIndex >= UrlQueue.Count)
                        {
                            LogMessageToFile("Index out of range");
                            break;
                        }

                        string url;
                        if (index < 0)
                        {
                            url = UrlQueue[^normalizedIndex];
                            UrlQueue.RemoveAt(UrlQueue.Count - normalizedIndex);
                        }
                        else
                        {
                            url = UrlQueue[normalizedIndex];
                            UrlQueue.RemoveAt(index);
                        }

                        LogMessageToFile($"Skipping {url}");
                        break;
                    }
                    case "tail":
                        LogMessageToFile(UrlQueue.Count == 0 ? "Queue is empty" : UrlQueue[^1]);
                        break;
                    #if DEBUG
                    case "test":
                        NormalizeUrlsInDb();
                        break;
                    #endif
                    case "v" or "version":
                        LogMessageToFile($"{Title} v{Version}");
                        break;
                    default:
                        QueueUrls(userInput);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "An unhanded exception occurred");
            }
        }
    }
}