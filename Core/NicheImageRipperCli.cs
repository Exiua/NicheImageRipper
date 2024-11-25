using Core.Enums;
using Core.SiteParsing;
using Serilog;
using Serilog.Events;

namespace Core;

public class NicheImageRipperCli : NicheImageRipper
{
    private async Task Rip()
    {
        while (UrlQueue.Count != 0)
        {
            var url = await RipUrl();
            UpdateHistory(Ripper, url);
        }
    }

    private void PrintHistory()
    {
        Console.WriteLine("+----------------------------------------+");
        Console.WriteLine("| Directory Name | URL | Date | Num URLs |");
        Console.WriteLine("+----------------------------------------+");
        foreach (var entry in History)
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
                LogMessageToFile("NicheImageRipper> ", newLine: false);
                var userInput = Console.ReadLine();
                if (string.IsNullOrEmpty(userInput))
                {
                    continue;
                }

                var cmdParts = userInput.Split(' ');
                switch (cmdParts[0])
                {
                    case "q":
                    case "quit":
                        LogMessageToFile("Exiting...");
                        SaveData();
                        HtmlParser.Dispose();
                        return;
                    case "r":
                    case "rip":
                        await Rip();
                        break;
                    case "queue":
                        LogMessageToFile(string.Join("\n", UrlQueue));
                        break;
                    case "c":
                    case "clear":
                        if (cmdParts.Length != 2)
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
                    case "retries":
                        if (cmdParts.Length != 2)
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
                    case "delay":
                        if(cmdParts.Length != 2)
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
                    case "skip":
                        if (UrlQueue.Count == 0)
                        {
                            LogMessageToFile("Queue is empty");
                            break;
                        }
                        
                        var index = 0;
                        if(cmdParts.Length >= 2)
                        {
                            if(int.TryParse(cmdParts[1], out var i))
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
                        if(normalizedIndex >= UrlQueue.Count)
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
                    case "debug":
                        HtmlParser.SetDebugMode(true);
                        Debugging = true;
                        break;
                    case "save":
                        SaveData();
                        LogMessageToFile("Data saved");
                        break;
                    case "history":
                        PrintHistory();
                        break;
                    case "l":
                    case "load":
                        LoadUrlFile(cmdParts.Length != 2 ? "UnfinishedRips.json" : cmdParts[1]);
                        LogMessageToFile("URLs loaded");
                        break;
                    case "peek":
                        LogMessageToFile(UrlQueue.Count == 0 ? "Queue is empty" : UrlQueue[0]);
                        break;
                    default:
                        var startIndex = UrlQueue.Count;
                        var offset = 0;
                        var failedUrls = QueueUrls(userInput);
                        foreach(var failedUrl in failedUrls)
                        {
                            switch (failedUrl.Reason)
                            {
                                case QueueFailureReason.None:
                                    break;
                                case QueueFailureReason.AlreadyQueued:
                                    LogMessageToFile($"URL already queued: {failedUrl.Url}");
                                    break;
                                case QueueFailureReason.NotSupported:
                                    LogMessageToFile($"URL not supported: {failedUrl.Url}");
                                    break;
                                case QueueFailureReason.PreviouslyProcessed:
                                    LogMessageToFile($"Re-rip url (y/n)? {failedUrl.Url}", newLine: false);
                                    var response = Console.ReadLine();
                                    if (response == "y")
                                    {
                                        var correctIndex = startIndex + failedUrl.Index + offset;
                                        offset++;
                                        UrlQueue.Insert(correctIndex, failedUrl.Url);
                                    }
                                    else
                                    {
                                        offset--;
                                    }
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }
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