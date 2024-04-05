using Core.FileDownloading;

namespace Core;

public class NicheImageRipperCli : NicheImageRipper
{
    protected override List<List<string>> GetHistoryData()
    {
        return History;
    }

    protected override void LoadHistory()
    {
        History = LoadHistoryData();
    }

    public override void UpdateHistory(ImageRipper ripper, string url)
    {
        var duplicate = History.FirstOrDefault(x => x[0] == ripper.FolderInfo.DirectoryName);
        if (duplicate != null)
        {
            History.Remove(duplicate);
            History.Add(duplicate); // Move to the end
        }
        else
        {
            var folderInfo = ripper.FolderInfo;
            History.Add([
                folderInfo.DirectoryName, url, DateTime.Now.ToString("yyyy-MM-dd"), folderInfo.NumUrls.ToString()
            ]);
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
            Console.WriteLine($"| {entry[0]} | {entry[1]} | {entry[2]} | {entry[3]} |");
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

                switch (userInput)
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
                        UrlQueue.Clear();
                        break;
                    case "save":
                        SaveData();
                        break;
                    case "history":
                        PrintHistory();
                        break;
                    case "l":
                    case "load":
                        var cmds = userInput.Split(' ');
                        List<string> unfinished;
                        unfinished = JsonUtility.Deserialize<List<string>>(cmds.Length != 2 ? "UnfinishedRips.json" : cmds[1]);
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