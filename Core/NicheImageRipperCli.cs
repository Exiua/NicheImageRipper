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
                                ClearCache();
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
                        LoadUrlFile(cmdParts.Length != 2 ? "UnfinishedRips.json" : cmdParts[1]);
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