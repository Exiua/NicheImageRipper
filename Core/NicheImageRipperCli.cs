using Core.Enums;
using Core.SiteParsing;

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
        while (true)
        {
            try
            {
                Console.Write("NicheImageRipper> ");
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
                        Console.WriteLine("Exiting...");
                        SaveData();
                        HtmlParser.Dispose();
                        return;
                    case "r":
                    case "rip":
                        await Rip();
                        break;
                    case "queue":
                        Console.WriteLine(string.Join("\n", UrlQueue));
                        break;
                    case "c":
                    case "clear":
                        if (cmdParts.Length != 2)
                        {
                            Console.WriteLine("Missing argument: cache or queue");
                            break;
                        }
                        
                        switch (cmdParts[1])
                        {
                            case "cache":
                                ClearCache();
                                Console.WriteLine("Cache cleared");
                                break;
                            case "queue":
                                UrlQueue.Clear();
                                Console.WriteLine("Queue cleared");
                                break;
                            default:
                                Console.WriteLine("Missing argument: cache or queue");
                                break;
                        }

                        break;
                    case "retries":
                        if (cmdParts.Length != 2)
                        {
                            Console.WriteLine($"Max retries: {MaxRetries - 1}");
                        }
                        else
                        {
                            if(int.TryParse(cmdParts[1], out var retries))
                            {
                                MaxRetries = retries + 1;
                                Console.WriteLine($"Max retries set to {MaxRetries}");
                            }
                            else
                            {
                                Console.WriteLine("Invalid argument");
                            }
                        }

                        break;
                    case "delay":
                        if(cmdParts.Length != 2)
                        {
                            Console.WriteLine($"Retry delay: {RetryDelay} ms");
                        }
                        else
                        {
                            if(int.TryParse(cmdParts[1], out var delay))
                            {
                                RetryDelay = delay;
                                Console.WriteLine($"Retry delay set to {delay} ms");
                            }
                            else
                            {
                                Console.WriteLine("Invalid argument");
                            }
                        }

                        break;
                    case "skip":
                        if (UrlQueue.Count == 0)
                        {
                            Console.WriteLine("Queue is empty");
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
                                Console.WriteLine("Invalid argument");
                                break;
                            }
                        }
                        
                        var normalizedIndex = Math.Abs(index);
                        if(normalizedIndex >= UrlQueue.Count)
                        {
                            Console.WriteLine("Index out of range");
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

                        Console.WriteLine($"Skipping {url}");
                        break;
                    case "debug":
                        HtmlParser.SetDebugMode(true);
                        Debugging = true;
                        break;
                    case "save":
                        SaveData();
                        Console.WriteLine("Data saved");
                        break;
                    case "history":
                        PrintHistory();
                        break;
                    case "l":
                    case "load":
                        LoadUrlFile(cmdParts.Length != 2 ? "UnfinishedRips.json" : cmdParts[1]);
                        Console.WriteLine("URLs loaded");
                        break;
                    case "peek":
                        Console.WriteLine(UrlQueue.Count == 0 ? "Queue is empty" : UrlQueue[0]);
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
                                    Console.WriteLine($"URL already queued: {failedUrl.Url}");
                                    break;
                                case QueueFailureReason.NotSupported:
                                    Console.WriteLine($"URL not supported: {failedUrl.Url}");
                                    break;
                                case QueueFailureReason.PreviouslyProcessed:
                                    Console.WriteLine($"Re-rip url (y/n)? {failedUrl.Url}");
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
                Console.WriteLine(e);
            }
        }
    }
}