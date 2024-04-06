using Core.DataStructures;
using Core.FileDownloading;
using Core.Utility;

namespace Core;

public class NicheImageRipperCli : NicheImageRipper
{
    public override void UpdateHistory(ImageRipper ripper, string url)
    {
        var duplicate = History.FirstOrDefault(x => x.DirectoryName == ripper.FolderInfo.DirectoryName);
        if (duplicate is not null)
        {
            History.Remove(duplicate);
            History.Add(duplicate); // Move to the end
        }
        else
        {
            var folderInfo = ripper.FolderInfo;
            History.Add(new HistoryEntry(folderInfo.DirectoryName, url, folderInfo.NumUrls));
        }
    }

    protected override void AddToUrlQueue(string url)
    {
        UrlQueue.Enqueue(url);
    }

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
                Console.Write("Enter a URL to rip or 'quit' to exit: ");
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
                        SaveData();
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
                        switch (cmdParts[1])
                        {
                            case "cache":
                                ClearCache();
                                break;
                            case "queue":
                                UrlQueue.Clear();
                                break;
                            default:
                                Console.WriteLine("Invalid command.");
                                break;
                        }

                        break;
                    case "save":
                        SaveData();
                        break;
                    case "history":
                        PrintHistory();
                        break;
                    case "l":
                    case "load":
                        var unfinished = JsonUtility.Deserialize<List<string>>(cmdParts.Length != 2 ? "UnfinishedRips.json" : cmdParts[1]);
                        if (unfinished is null)
                        {
                            Console.WriteLine("No unfinished rips found.");
                            break;
                        }
                        foreach (var url in unfinished)
                        {
                            AddToUrlQueue(url);
                        }
                        break;
                    default:
                        QueueUrls(userInput);
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